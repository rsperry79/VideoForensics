using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using System.Collections.ObjectModel;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Exceptions;
using LocationMetadata = VideoForensics.Data.Common.Entities.LocationMetadata;
using SyncStatus = VideoForensics.Data.Common.Entities.SyncStatus;
using VideoForensics.Data.Core.Services;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingDeviceDiscoveryService : IDeviceDiscoveryService, ILocationMetadataPersister
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly ILocationMetadataRepository? _metadataRepository;
        private readonly ILocationRepository? _locationRepository;
        private readonly ApiResponseNormalizer? _normalizer;
        private readonly RingAccountSettingsPersister? _settingsPersister;
        private readonly IRingEventHistoryWriter? _eventHistoryWriter;

        // Sentinel used when a device's own location_id is missing from Ring's API response. Never
        // fall back to the locationId that was requested here - the legacy ring_devices endpoint's
        // location_id query param doesn't reliably filter, so a device with a genuinely missing
        // location_id would otherwise get silently mis-attributed to whichever location the caller
        // happened to be enumerating at the time.
        public const string UnknownLocationId = "unknown";

        // Long enough to span a full interactive download flow: MenuManager fetches the device list
        // once up front to render the table, then the pre-scan/download path fetches it again via
        // its own DiscoverUniqueDevicesAsync - separated by however long the user takes answering
        // the download-location, start-date, and force-rescan prompts in between. At the old 30s TTL
        // that gap alone reliably expired the cache and triggered a fully redundant GetLocationsAsync
        // + device-list fetch right before the run that most needs to conserve API calls. A ring
        // camera's device/location list essentially never changes mid-session, so a stale read for a
        // few minutes is a non-issue here.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private readonly SemaphoreSlim _locationsCacheLock = new(1, 1);
        private IReadOnlyList<Location>? _cachedLocations;
        private DateTime _cachedLocationsAt;

        public RingDeviceDiscoveryService(
            ILogger logger,
            ISessionProvider sessionProvider,
            ILocationMetadataRepository? metadataRepository = null,
            ApiResponseNormalizer? normalizer = null,
            ILocationRepository? locationRepository = null,
            RingAccountSettingsPersister? settingsPersister = null,
            IRingEventHistoryWriter? eventHistoryWriter = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _metadataRepository = metadataRepository;
            _normalizer = normalizer;
            _locationRepository = locationRepository;
            _settingsPersister = settingsPersister;
            _eventHistoryWriter = eventHistoryWriter;
        }

        public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
        {
            await _locationsCacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedLocations != null && DateTime.UtcNow - _cachedLocationsAt < CacheTtl)
                {
                    _logger.LogInformation("Reusing cached locations ({Count}), fetched {Age:F0}s ago",
                        _cachedLocations.Count, (DateTime.UtcNow - _cachedLocationsAt).TotalSeconds);
                    return _cachedLocations;
                }

                _logger.LogInformation("Fetching Ring locations");

#pragma warning disable CS0618
                Session? session = _sessionProvider.GetSession();
#pragma warning restore CS0618
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Location>().AsReadOnly();
                }

                Debug.Assert(session != null, "Session must not be null after null check");
                _logger.LogInformation("Session exists: OAuthToken = {HasToken}",
                    session.OAuthToken != null ? "yes" : "no");

                // Ensure session is valid before calling APIs
                try
                {
                    await session.EnsureSessionValid();
                    _logger.LogInformation("Session validation passed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Session validation failed");
                    throw;
                }

                List<Entities.Location>? locations = await session.GetLocations();
                _logger.LogInformation("GetLocations() completed, returned collection type: {Type}", locations?.GetType().Name ?? "null");

                if (locations == null)
                {
                    _logger.LogWarning("GetLocations returned null");
                    return new List<Location>().AsReadOnly();
                }

                _logger.LogInformation("GetLocations returned {RawLocationCount} location(s)", locations.Count);

                IReadOnlyList<Location> result = locations
                    .Where(l => l.Id.HasValue)
                    .Select(ToContractLocation)
                    .ToList()
                    .AsReadOnly();

                // The devices/v1/locations endpoint has been observed returning an empty list for
                // some accounts/tokens even when the account genuinely owns devices (it's a newer
                // endpoint than the legacy ring_devices one GetRingDevices uses, and doesn't always
                // carry the same access). Fall back to deriving locations directly from the device
                // list itself so device discovery doesn't dead-end on that endpoint alone.
                if (result.Count == 0)
                {
                    _logger.LogWarning("GetLocations returned zero locations; deriving locations from the device list instead");
                    result = await DeriveLocationsFromDevicesAsync(cancellationToken);
                }
                else
                {
                    // Some accounts have a location whose location_id came back malformed/missing
                    // in this response (the l.Id.HasValue filter above drops it) even though its
                    // devices' own location_id fields are perfectly valid — without this, those
                    // devices have no matching Location.Id anywhere and callers that key off it
                    // (e.g. attributing a device to a location name) silently mis-attribute them to
                    // whichever other location happens to be checked first. Fill the gap with a
                    // synthetic Location per orphaned device location_id, so the device still lands
                    // under something honestly labeled instead of a wrong real location name.
                    var knownIds = new HashSet<string>(result.Select(l => l.Id));
                    IReadOnlyList<Location> derived = await DeriveLocationsFromDevicesAsync(cancellationToken);
                    var orphaned = derived.Where(l => !knownIds.Contains(l.Id)).ToList();
                    if (orphaned.Count > 0)
                    {
                        _logger.LogWarning("Found {Count} device location_id(s) not present in the locations list; adding synthetic entries", orphaned.Count);
                        result = result.Concat(orphaned).ToList().AsReadOnly();
                    }
                }

                _logger.LogInformation("Found {LocationCount} locations after filtering", result.Count);
                foreach (Location loc in result)
                {
                    _logger.LogInformation("Location: {LocationId} - {LocationName}", loc.Id, loc.Name);
                }

                _cachedLocations = result;
                _cachedLocationsAt = DateTime.UtcNow;

                // Capture location metadata as audit trail (non-critical, fire-and-forget)
                _ = Task.Run(() => PersistLocationMetadataAsync(result, cancellationToken), cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations from Ring API: {Message}", ex.Message);
                throw;
            }
            finally
            {
                _ = _locationsCacheLock.Release();
            }
        }

        /// <summary>
        /// Builds a location list from the location_id each device already reports, for accounts
        /// where devices/v1/locations comes back empty, or to fill in a location_id present on
        /// devices but missing/malformed in that response. Reuses the cached full device list
        /// (GetAllDevicesUnfilteredAsync) rather than issuing its own ring_devices call - on an
        /// account already close to Ring's rate limit, every avoidable request matters. Devices
        /// with no resolvable location_id (UnknownLocationId) are skipped since there's nothing to
        /// group them under.
        /// </summary>
        /// <summary>
        /// Carries Ring's raw device <c>kind</c> (e.g. doorbell_v4), so the stored device type can be derived from it,
        /// and its <c>time_zone</c>, so Devices.TimeZoneId can be stored. Missing values are omitted, not blanked.
        /// </summary>
        private static Dictionary<string, string>? DeviceMetadata(string? kind, string? timeZone)
        {
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(kind))
            {
                metadata["kind"] = kind;
            }

            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                metadata["time_zone"] = timeZone;
            }

            return metadata.Count > 0 ? metadata : null;
        }

        /// <summary>
        /// Maps a Ring location to the provider-agnostic contract, keeping the address parts,
        /// timezone and ownership in <see cref="Location.Metadata"/> for LocationMetadata persistence.
        /// </summary>
        internal static Location ToContractLocation(Entities.Location l)
        {
            var metadata = new Dictionary<string, string>();
            AddIfPresent(metadata, "city", l.Address?.City);
            AddIfPresent(metadata, "state", l.Address?.State);
            AddIfPresent(metadata, "zip_code", l.Address?.ZipCode);
            AddIfPresent(metadata, "timezone", l.Address?.TimeZone);
            if (l.IsOwner.HasValue)
            {
                metadata["is_owner"] = l.IsOwner.Value ? "true" : "false";
            }

            return new Location(
                Id: l.Id!.Value.ToString(),
                Name: l.Name ?? "Unknown Location",
                Address: l.Address?.Address1,
                Metadata: metadata.Count > 0 ? metadata : null);
        }

        private static void AddIfPresent(Dictionary<string, string> metadata, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        private async Task<IReadOnlyList<Location>> DeriveLocationsFromDevicesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Device> devices = await GetAllDevicesUnfilteredAsync(cancellationToken);

            return devices
                .Where(d => d.LocationId != UnknownLocationId)
                .GroupBy(d => d.LocationId)
                .Select(g => new Location(
                    Id: g.Key,
                    Name: "Unknown Location",
                    Address: null
                ))
                .ToList()
                .AsReadOnly();
        }

        // The account's full, unfiltered device list, cached once regardless of which location it
        // was fetched under. Ring's legacy ring_devices?location_id= query param doesn't actually
        // filter server-side (confirmed: requesting under any one of an account's locations returns
        // every device on the account, not just that location's) - so calling it once per location,
        // as this used to, sent the same expensive request N times per run for no benefit. On an
        // account already skating close to Ring's rate limit, that's real, avoidable load.
        private readonly SemaphoreSlim _allDevicesCacheLock = new(1, 1);
        private (IReadOnlyList<Device> Devices, DateTime FetchedAt)? _cachedAllDevices;

        public async Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Device> allDevices = await GetAllDevicesUnfilteredAsync(cancellationToken);
            return allDevices.Where(d => d.LocationId == locationId).ToList().AsReadOnly();
        }

        private async Task<IReadOnlyList<Device>> GetAllDevicesUnfilteredAsync(CancellationToken cancellationToken)
        {
            await _allDevicesCacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedAllDevices.HasValue && DateTime.UtcNow - _cachedAllDevices.Value.FetchedAt < CacheTtl)
                {
                    _logger.LogInformation("Reusing cached device list ({Count}), fetched {Age:F0}s ago",
                        _cachedAllDevices.Value.Devices.Count, (DateTime.UtcNow - _cachedAllDevices.Value.FetchedAt).TotalSeconds);
                    return _cachedAllDevices.Value.Devices;
                }

                _logger.LogInformation("Fetching the account's full device list");

#pragma warning disable CS0618
                Session? session = _sessionProvider.GetSession();
#pragma warning restore CS0618
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Device>().AsReadOnly();
                }

                Debug.Assert(session != null, "Session must not be null after null check");
                Entities.Devices? devices = await session.GetRingDevices();

                var deviceMap = new Dictionary<string, Device>();

                // Add Doorbots
                // Id uses the numeric doorbot id (not the hex device_id) because that's the only
                // identifier the /doorbots/history endpoint's embedded doorbot object populates —
                // history events can't be matched back to a device by device_id (it comes back empty).
                if (devices?.Doorbots != null)
                {
                    foreach (Entities.Doorbot d in devices.Doorbots)
                    {
                        string deviceId = d.Id.ToString();
                        deviceMap[deviceId] = new Device(
                            Id: deviceId,
                            Name: d.Description ?? "Unknown Device",
                            Type: "doorbot",
                            LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                            IsOnline: d.Health?.Connected ?? d.Subscribed,
                            Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                            Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                        );
                    }
                }

                // Add Stickup Cameras (skip if already added via Doorbots)
                if (devices?.StickupCams != null)
                {
                    foreach (Entities.StickupCam d in devices.StickupCams)
                    {
                        string deviceId = d.Id?.ToString() ?? d.DeviceId;
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "stickup_cam",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Health?.Connected ?? d.Subscribed,
                                Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                            );
                        }
                    }
                }

                // Add Authorized Doorbots (skip if already added)
                if (devices?.AuthorizedDoorbots != null)
                {
                    foreach (Entities.Doorbot d in devices.AuthorizedDoorbots)
                    {
                        string deviceId = d.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "authorized_doorbot",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Health?.Connected ?? d.Subscribed,
                                Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                            );
                        }
                    }
                }

                // Add Chimes. Chimes have no video/event history (there's nothing for
                // VideoDownloadServiceAdapter to download from one), but they're still a real
                // device on the account and belong in the Devices table for forensic completeness -
                // see DbCompletenessChecker, which flags a chime present on the account but absent
                // from the DB. IsOnline uses Health.Connected (chimes have no Subscribed field).
                if (devices?.Chimes != null)
                {
                    foreach (Entities.Chime c in devices.Chimes)
                    {
                        string deviceId = c.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: c.Description ?? "Unknown Device",
                                Type: "chime",
                                LocationId: c.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: c.Health?.Connected,
                                Metadata: DeviceMetadata(c.Kind, c.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(c)
                            );
                        }
                    }
                }

                var allAccountDevices = new List<Device>(deviceMap.Values);

                _logger.LogInformation("Found {DeviceCount} devices on the account", allAccountDevices.Count);
                foreach (Device device in allAccountDevices)
                {
                    _logger.LogInformation("  Device: {DeviceId} - {DeviceName} ({DeviceType}), Location: {LocationId}, Online: {IsOnline}",
                        device.Id, device.Name, device.Type, device.LocationId, device.IsOnline);
                }

                ReadOnlyCollection<Device> readOnlyDevices = allAccountDevices.AsReadOnly();
                _cachedAllDevices = (readOnlyDevices, DateTime.UtcNow);
                return readOnlyDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching the account's device list");
                return new List<Device>().AsReadOnly();
            }
            finally
            {
                _ = _allDevicesCacheLock.Release();
            }
        }

        public async Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching device: {DeviceId}", deviceId);

                IReadOnlyList<Location> locations = await GetLocationsAsync(cancellationToken);

                foreach (Location location in locations)
                {
                    IReadOnlyList<Device> devices = await GetDevicesAsync(location.Id, cancellationToken);
                    Device? device = devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        return device;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device {DeviceId}", deviceId);
                return null;
            }
        }

        // ===== Account-Aware Overloads (Phase 1 Refactoring) =====
        // These methods enable concurrent processing of multiple Ring accounts without race conditions
        // by accepting an explicit providerAccountId and passing it to SessionProvider.GetSession(Guid)

        /// <summary>
        /// Gets all locations/sites for the authenticated user in a specific provider account (account-aware overload).
        /// </summary>
        public async Task<IReadOnlyList<Location>> GetLocationsAsync(Guid providerAccountId, CancellationToken cancellationToken = default)
        {
            await _locationsCacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedLocations != null && DateTime.UtcNow - _cachedLocationsAt < CacheTtl)
                {
                    _logger.LogInformation("Reusing cached Ring locations ({Count}), fetched {Age:F0}s ago",
                        _cachedLocations.Count, (DateTime.UtcNow - _cachedLocationsAt).TotalSeconds);
                    return _cachedLocations;
                }

                _logger.LogInformation("Fetching Ring locations");

                Session? session = _sessionProvider.GetSession(providerAccountId);
                if (session == null)
                {
                    _logger.LogError("Not authenticated for Ring account: Session is null");
                    return new List<Location>().AsReadOnly();
                }

                _logger.LogInformation("Ring session exists: OAuthToken = {HasToken}",
                    session.OAuthToken != null ? "yes" : "no");

                // Ensure session is valid before calling APIs
                try
                {
                    await session.EnsureSessionValid();
                    _logger.LogInformation("Ring session validation passed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ring session validation failed");
                    throw;
                }

                List<Entities.Location>? locations = await session.GetLocations();
                _logger.LogInformation("GetLocations() completed for account {AccountId}, returned collection type: {Type}",
                    providerAccountId, locations?.GetType().Name ?? "null");

                if (locations == null)
                {
                    _logger.LogWarning("GetLocations returned null for account {AccountId}", providerAccountId);
                    return new List<Location>().AsReadOnly();
                }

                _logger.LogInformation("GetLocations returned {RawLocationCount} location(s) for account {AccountId}",
                    locations.Count, providerAccountId);

                IReadOnlyList<Location> result = locations
                    .Where(l => l.Id.HasValue)
                    .Select(ToContractLocation)
                    .ToList()
                    .AsReadOnly();

                if (result.Count == 0)
                {
                    _logger.LogWarning("GetLocations returned zero locations for account {AccountId}; deriving from device list", providerAccountId);
                    result = await DeriveLocationsFromDevicesAsync(providerAccountId, cancellationToken);
                }
                else
                {
                    var knownIds = new HashSet<string>(result.Select(l => l.Id));
                    IReadOnlyList<Location> derived = await DeriveLocationsFromDevicesAsync(providerAccountId, cancellationToken);
                    var orphaned = derived.Where(l => !knownIds.Contains(l.Id)).ToList();
                    if (orphaned.Count > 0)
                    {
                        _logger.LogWarning("Found {Count} orphaned device location_id(s) for account {AccountId}", orphaned.Count, providerAccountId);
                        result = result.Concat(orphaned).ToList().AsReadOnly();
                    }
                }

                _logger.LogInformation("Found {LocationCount} locations for account {AccountId}", result.Count, providerAccountId);
                _cachedLocations = result;
                _cachedLocationsAt = DateTime.UtcNow;

                // Once per fresh fetch (a cache hit returns above, so a sync fetches account settings once).
                await PersistAccountSettingsAsync(session, providerAccountId, result, cancellationToken);

                // Runs whether or not the settings persister is configured, and after it, so a settings failure never blocks the event feeds.
                await PersistEventHistoryAsync(session, providerAccountId, result, cancellationToken);

                _ = Task.Run(() => PersistLocationMetadataAsync(result, cancellationToken), cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations for account {AccountId}: {Message}", providerAccountId, ex.Message);
                throw;
            }
            finally
            {
                _ = _locationsCacheLock.Release();
            }
        }

        /// <summary>
        /// Fetches and persists the account's profile and ringtones, then each location's light groups, location
        /// mode, mode settings and mode sharing. Each fetch is isolated: a failure is logged and the rest still run.
        /// The profile's authentication token is never read, logged or passed on.
        /// </summary>
        private async Task PersistAccountSettingsAsync(Session session, Guid providerAccountId, IReadOnlyList<Location> locations, CancellationToken ct)
        {
            if (_settingsPersister == null)
            {
                return;
            }

            Entities.Profile? profile = await FetchForSyncAsync("profile", () => session.GetProfile(), ct);
            if (profile != null)
            {
                await _settingsPersister.PersistProfileAsync(profile, providerAccountId, ct);
            }

            Entities.RingtonesResponse? ringtones = await FetchForSyncAsync("ringtone", () => session.GetRingtones(), ct);
            if (ringtones != null)
            {
                await _settingsPersister.PersistRingtonesAsync(ringtones, providerAccountId, ct);
            }

            foreach (Location location in locations)
            {
                if (!Guid.TryParse(location.Id, out Guid locationGuid))
                {
                    _logger.LogWarning("Skipping Ring location settings: location id is not a GUID");
                    continue;
                }

                Entities.GroupsResponse? groups = await FetchForSyncAsync("light group", () => session.GetGroups(locationGuid, ct), ct);
                if (groups != null)
                {
                    await _settingsPersister.PersistLightGroupsAsync(groups, location.Id, ct);
                }

                // A 404 means the location has no mode data (for example no Location Modes); that is expected, not a fault.
                Entities.LocationMode? mode = await FetchForSyncAsync("location mode", () => session.GetLocationMode(locationGuid), ct, notFoundIsExpected: true);
                if (mode != null)
                {
                    await _settingsPersister.PersistLocationModeAsync(mode, location.Id, ct);
                }

                Entities.LocationModeSettingsResponse? modeSettings = await FetchForSyncAsync("location mode settings", () => session.GetLocationModeSettings(locationGuid), ct, notFoundIsExpected: true);
                if (modeSettings != null)
                {
                    await _settingsPersister.PersistLocationModeSettingsAsync(modeSettings, location.Id, ct);
                }

                Entities.LocationModeSharingResponse? sharing = await FetchForSyncAsync("location mode sharing", () => session.GetLocationModeSharing(locationGuid), ct, notFoundIsExpected: true);
                if (sharing != null)
                {
                    await _settingsPersister.PersistLocationModeSharingAsync(sharing, location.Id, ct);
                }
            }
        }

        /// <summary>
        /// Runs one settings fetch and returns null on failure. A 404 is logged at information level when
        /// <paramref name="notFoundIsExpected"/> is set (the location has no such data), otherwise as a warning.
        /// Cancellation is rethrown. Only the settings area is logged, never an account or session identifier.
        /// </summary>
        private async Task<T?> FetchForSyncAsync<T>(string area, Func<Task<T>> fetch, CancellationToken ct, bool notFoundIsExpected = false) where T : class
        {
            try
            {
                return await fetch();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (DeviceUnknownException) when (notFoundIsExpected)
            {
                _logger.LogInformation("Ring {Area} not available for this location (not found); skipped", area);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ring {Area} fetch failed; sync continues", area);
                return null;
            }
        }

        /// <summary>
        /// Fetches and persists an account's event-history feeds: doorbot history and video search for each doorbot,
        /// then location events, evm location history and app location history for each location. Every step runs
        /// through <see cref="SyncFeedAsync"/>, so one failing feed or doorbot never stops the others. All calls are reads.
        /// </summary>
        private async Task PersistEventHistoryAsync(Session session, Guid providerAccountId, IReadOnlyList<Location> locations, CancellationToken ct)
        {
            IRingEventHistoryWriter? eventWriter = _eventHistoryWriter;
            if (eventWriter == null)
            {
                return;
            }

            IReadOnlyList<Device> devices = [];
            await SyncFeedAsync("device list", async () =>
            {
                devices = await GetAllDevicesUnfilteredAsync(providerAccountId, ct);
            }, ct);

            foreach (Device device in devices)
            {
                // Chimes have no doorbot history or video search; the other device ids are numeric doorbot ids.
                if (device.Type == "chime" || !long.TryParse(device.Id, out long doorbotId))
                {
                    continue;
                }

                await SyncFeedAsync("doorbot history", () => PersistDoorbotHistoryAsync(session, doorbotId, device.Name, eventWriter, ct), ct);

                await SyncFeedAsync("video search", async () =>
                {
                    List<Entities.VideoSearchItem> items = await session.VideoSearch(doorbotId, cancellationToken: ct);
                    foreach (Entities.VideoSearchItem item in items)
                    {
                        var mapped = RingEventHistoryMapper.MapVideoSearch(item, providerAccountId);
                        await eventWriter.ReplaceVideoSearchEventAsync(providerAccountId, mapped.Event, mapped.Details, ct);
                    }
                }, ct);
            }

            foreach (Location location in locations)
            {
                if (!Guid.TryParse(location.Id, out Guid locationGuid))
                {
                    _logger.LogWarning("Skipping Ring event history: location id is not a GUID");
                    continue;
                }

                await SyncFeedAsync("location events", async () =>
                {
                    Entities.LocationEventsResponse? page = await session.GetLocationEventsResponse(locationGuid);
                    if (page is null)
                    {
                        return;
                    }

                    await eventWriter.ReplaceLocationEventPageAsync(RingEventHistoryMapper.MapLocationEventsPage(location.Id, page.Meta), ct);
                    foreach (Entities.LocationEvent evt in page.Events ?? [])
                    {
                        var mapped = RingEventHistoryMapper.MapLocationEvent(evt, location.Id);
                        await eventWriter.ReplaceLocationEventAsync(mapped.Event, mapped.Details, ct);
                    }
                }, ct);

                await SyncFeedAsync("location history", async () =>
                {
                    JsonElement raw = await session.GetLocationHistory(locationGuid, ct);
                    Entities.LocationHistoryResponse? response = raw.Deserialize<Entities.LocationHistoryResponse>();
                    if (response is null)
                    {
                        return;
                    }

                    await eventWriter.ReplaceLocationHistoryPageAsync(RingEventHistoryMapper.MapLocationHistoryPage(location.Id, response.PaginationKey), ct);
                    foreach (Entities.LocationHistoryItem item in response.Items ?? [])
                    {
                        var mapped = RingEventHistoryMapper.MapLocationHistoryItem(item, location.Id);
                        await eventWriter.ReplaceLocationHistoryEventAsync(mapped.Event, mapped.Details, ct);
                    }
                }, ct);

                await SyncFeedAsync("app location history", async () =>
                {
                    string json = await session.GetAppLocationHistoryAsync(locationGuid, cancellationToken: ct);
                    List<Entities.AppLocationHistoryMessage>? messages = JsonSerializer.Deserialize<List<Entities.AppLocationHistoryMessage>>(json);
                    foreach (Entities.AppLocationHistoryMessage message in messages ?? [])
                    {
                        await eventWriter.ReplaceAppLocationHistoryMessageAsync(RingEventHistoryMapper.MapAppLocationHistory(message), ct);
                    }
                }, ct);
            }
        }

        /// <summary>Items requested per doorbot history page; a page shorter than this is the last one.</summary>
        private const int DoorbotHistoryPageSize = 100;

        /// <summary>Upper bound on history pages fetched per doorbot in one sync, so one busy doorbell cannot hold a sync.</summary>
        private const int DoorbotHistoryPageCap = 200;

        /// <summary>
        /// Pages one doorbot's full history newest-first and persists each page before requesting the next, so a
        /// long history never sits in memory. Stops on an empty page, a short page, a page whose oldest id does not
        /// move past the cursor (a repeat would loop forever), or after <see cref="DoorbotHistoryPageCap"/> pages.
        /// The cap logs the doorbell name only; the remaining history is fetched on the next sync.
        /// </summary>
        private async Task PersistDoorbotHistoryAsync(Session session, long doorbotId, string doorbellName, IRingEventHistoryWriter eventWriter, CancellationToken ct)
        {
            long? cursor = null;
            for (int pageNumber = 1; ; pageNumber++)
            {
                List<Entities.DoorbotHistoryEvent> page = await session.GetDoorbotsHistoryPageAsync(doorbotId, DoorbotHistoryPageSize, cursor, ct);
                if (page.Count == 0)
                {
                    return;
                }

                // Ring pages newest-first, so the oldest id on this page is the cursor for the next one.
                long? oldestId = page.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).Cast<long?>().Min();
                if (cursor.HasValue && oldestId.HasValue && oldestId.Value >= cursor.Value)
                {
                    _logger.LogWarning("Ring doorbot history paging for {DoorbellName} stopped: the page did not move past the previous cursor", doorbellName);
                    return;
                }

                foreach (Entities.DoorbotHistoryEvent evt in page)
                {
                    var mapped = RingEventHistoryMapper.MapDoorbotHistoryEvent(evt);
                    await eventWriter.ReplaceDoorbotHistoryEventAsync(mapped.Event, mapped.Details, ct);
                }

                if (page.Count < DoorbotHistoryPageSize || !oldestId.HasValue)
                {
                    return;
                }

                if (pageNumber == DoorbotHistoryPageCap)
                {
                    _logger.LogInformation("Ring doorbot history for {DoorbellName} reached the {PageCap}-page sync cap; the rest will be fetched on the next sync",
                        doorbellName, DoorbotHistoryPageCap);
                    return;
                }

                cursor = oldestId;
            }
        }

        /// <summary>
        /// Runs one event-history fetch-and-persist step. A 404 is logged at information level and skipped; any other
        /// failure is logged as a warning that names only the feed, and the sync continues. Cancellation is rethrown.
        /// </summary>
        private async Task SyncFeedAsync(string feed, Func<Task> fetchAndPersist, CancellationToken ct)
        {
            try
            {
                await fetchAndPersist();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (DeviceUnknownException)
            {
                _logger.LogInformation("Ring {Feed} not available (not found); skipped", feed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ring {Feed} sync failed; sync continues", feed);
            }
        }

        /// <summary>
        /// Derives a location list from device location_ids for a specific account (account-aware overload).
        /// </summary>
        private async Task<IReadOnlyList<Location>> DeriveLocationsFromDevicesAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            IReadOnlyList<Device> devices = await GetAllDevicesUnfilteredAsync(providerAccountId, cancellationToken);

            return devices
                .Where(d => d.LocationId != UnknownLocationId)
                .GroupBy(d => d.LocationId)
                .Select(g => new Location(
                    Id: g.Key,
                    Name: "Unknown Location",
                    Address: null
                ))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets all devices at a specific location for a specific provider account (account-aware overload).
        /// </summary>
        public async Task<IReadOnlyList<Device>> GetDevicesAsync(Guid providerAccountId, string locationId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Device> allDevices = await GetAllDevicesUnfilteredAsync(providerAccountId, cancellationToken);
            return allDevices.Where(d => d.LocationId == locationId).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all devices (unfiltered) for a specific provider account, with caching per account (account-aware overload).
        /// </summary>
        private async Task<IReadOnlyList<Device>> GetAllDevicesUnfilteredAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            await _allDevicesCacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedAllDevices.HasValue && DateTime.UtcNow - _cachedAllDevices.Value.FetchedAt < CacheTtl)
                {
                    _logger.LogInformation("Reusing cached device list ({Count}) for account {AccountId}, fetched {Age:F0}s ago",
                        _cachedAllDevices.Value.Devices.Count, providerAccountId, (DateTime.UtcNow - _cachedAllDevices.Value.FetchedAt).TotalSeconds);
                    return _cachedAllDevices.Value.Devices;
                }

                _logger.LogInformation("Fetching device list for account {AccountId}", providerAccountId);

                Session? session = _sessionProvider.GetSession(providerAccountId);
                if (session == null)
                {
                    _logger.LogError("Not authenticated for Ring account: Session is null");
                    return new List<Device>().AsReadOnly();
                }

                Entities.Devices? devices = await session.GetRingDevices();

                var deviceMap = new Dictionary<string, Device>();

                if (devices?.Doorbots != null)
                {
                    foreach (Entities.Doorbot d in devices.Doorbots)
                    {
                        string deviceId = d.Id.ToString();
                        deviceMap[deviceId] = new Device(
                            Id: deviceId,
                            Name: d.Description ?? "Unknown Device",
                            Type: "doorbot",
                            LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                            IsOnline: d.Health?.Connected ?? d.Subscribed,
                            Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                            Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                        );
                    }
                }

                if (devices?.AuthorizedDoorbots != null)
                {
                    foreach (Entities.Doorbot d in devices.AuthorizedDoorbots)
                    {
                        string deviceId = d.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "doorbot",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Health?.Connected ?? d.Subscribed,
                                Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                            );
                        }
                    }
                }

                if (devices?.StickupCams != null)
                {
                    foreach (Entities.StickupCam s in devices.StickupCams)
                    {
                        string deviceId = s.Id?.ToString() ?? s.DeviceId;
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: s.Description ?? "Unknown Device",
                                Type: "stickup_cam",
                                LocationId: s.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: s.Health?.Connected ?? s.Subscribed,
                                Metadata: DeviceMetadata(s.Kind, s.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(s)
                            );
                        }
                    }
                }

                if (devices?.AuthorizedDoorbots != null)
                {
                    foreach (Entities.Doorbot d in devices.AuthorizedDoorbots)
                    {
                        string deviceId = d.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "authorized_doorbot",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Health?.Connected ?? d.Subscribed,
                                Metadata: DeviceMetadata(d.Kind, d.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(d)
                            );
                        }
                    }
                }

                if (devices?.Chimes != null)
                {
                    foreach (Entities.Chime c in devices.Chimes)
                    {
                        string deviceId = c.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: c.Description ?? "Unknown Device",
                                Type: "chime",
                                LocationId: c.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: c.Health?.Connected,
                                Metadata: DeviceMetadata(c.Kind, c.TimeZone),
                                Details: RingDeviceDetailsMapper.ToDeviceFields(c)
                            );
                        }
                    }
                }

                IReadOnlyList<Device> result = deviceMap.Values.ToList().AsReadOnly();
                _logger.LogInformation("Found {DeviceCount} devices for account {AccountId}", result.Count, providerAccountId);

                _cachedAllDevices = (result, DateTime.UtcNow);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device list for account {AccountId}: {Message}", providerAccountId, ex.Message);
                throw;
            }
            finally
            {
                _ = _allDevicesCacheLock.Release();
            }
        }

        /// <summary>
        /// Gets a specific device for a specific provider account (account-aware overload).
        /// </summary>
        public async Task<Device?> GetDeviceAsync(Guid providerAccountId, string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching device {DeviceId} for account {AccountId}", deviceId, providerAccountId);

                IReadOnlyList<Location> locations = await GetLocationsAsync(providerAccountId, cancellationToken);

                foreach (Location location in locations)
                {
                    IReadOnlyList<Device> devices = await GetDevicesAsync(providerAccountId, location.Id, cancellationToken);
                    Device? device = devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        return device;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device {DeviceId} for account {AccountId}", deviceId, providerAccountId);
                return null;
            }
        }

        /// <summary>
        /// Captures and persists location metadata as an audit trail of location discovery.
        /// Runs as a fire-and-forget background task for forensic completeness.
        /// </summary>
        public async Task PersistLocationMetadataAsync(IReadOnlyList<Location> locations, CancellationToken ct = default)
        {
            if (_metadataRepository == null)
            {
                _logger.LogDebug("Location metadata repository not available; skipping metadata capture");
                return;
            }

            try
            {
                foreach (Location location in locations)
                {
                    if (string.IsNullOrEmpty(location.Id))
                    {
                        _logger.LogWarning("Location has no ID; skipping metadata capture for this location");
                        continue;
                    }

                    // LocationMetadata.LocationId must be the real Locations.Id, not a hash of the provider id:
                    // Locations.Id is a random Guid assigned when the Location row is created, so a hashed id
                    // never joins. The Location row is created after discovery (by the download adapter), so
                    // skip until it exists; a later discovery pass persists it.
                    VideoForensics.Data.Common.Entities.Location? dbLocation = _locationRepository == null
                        ? null
                        : await _locationRepository.GetByProviderLocationIdAsync(location.Id, ct);
                    if (dbLocation == null)
                    {
                        _logger.LogDebug("Location row not stored yet for location {LocationId}; metadata capture deferred", location.Id);
                        continue;
                    }

                    Guid locationGuid = dbLocation.Id;

                    LocationMetadata? existing = await _metadataRepository.GetByLocationIdAsync(locationGuid, ct);
                    if (existing?.LastSyncedUtc > DateTime.UtcNow.AddMinutes(-5))
                    {
                        _logger.LogDebug("Location metadata recently captured for location {LocationId}", location.Id);
                        continue;
                    }

                    LocationMetadata metadata = existing ?? new LocationMetadata { Id = Guid.NewGuid(), LocationId = locationGuid };
                    metadata.StreetAddress = location.Address;
                    metadata.City = MetadataValue(location, "city");
                    metadata.State = MetadataValue(location, "state");
                    metadata.PostalCode = MetadataValue(location, "zip_code");
                    metadata.TimeZoneId = MetadataValue(location, "timezone");
                    metadata.IsOwner = bool.TryParse(MetadataValue(location, "is_owner"), out bool isOwner) ? isOwner : null;
                    metadata.LastSyncedUtc = DateTime.UtcNow;
                    metadata.SyncStatus = SyncStatus.Pending;

                    if (existing == null)
                    {
                        await _metadataRepository.AddAsync(metadata, ct);
                    }
                    else
                    {
                        await _metadataRepository.UpdateAsync(metadata, ct);
                    }

                    _logger.LogDebug("Captured metadata for location {LocationId}", location.Id);
                }

                _logger.LogDebug("Location metadata capture completed for {LocationCount} location(s)", locations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping location metadata capture (non-critical operation failed)");
            }
        }

        private static string? MetadataValue(Location location, string key)
        {
            return location.Metadata != null && location.Metadata.TryGetValue(key, out string? value) ? value : null;
        }
    }
}
