using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingDeviceDiscoveryService : IDeviceDiscoveryService
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDeviceCapabilitiesRepository? _capabilitiesRepository;
        private readonly ILocationMetadataRepository? _metadataRepository;
        private readonly ApiResponseNormalizer? _normalizer;

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
            IDeviceCapabilitiesRepository? capabilitiesRepository = null,
            ILocationMetadataRepository? metadataRepository = null,
            ApiResponseNormalizer? normalizer = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _capabilitiesRepository = capabilitiesRepository;
            _metadataRepository = metadataRepository;
            _normalizer = normalizer;
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

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Location>().AsReadOnly();
                }

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

                var locations = await session.GetLocations();
                _logger.LogInformation("GetLocations() completed, returned collection type: {Type}", locations?.GetType().Name ?? "null");

                if (locations == null)
                {
                    _logger.LogWarning("GetLocations returned null");
                    return new List<Location>().AsReadOnly();
                }

                _logger.LogInformation("GetLocations returned {RawLocationCount} location(s)", locations.Count);

                IReadOnlyList<Location> result = locations
                    .Where(l => l.Id.HasValue)
                    .Select(l => new Location(
                        Id: l.Id!.Value.ToString(),
                        Name: l.Name ?? "Unknown Location",
                        Address: l.Address?.Address1
                    ))
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
                    var derived = await DeriveLocationsFromDevicesAsync(cancellationToken);
                    var orphaned = derived.Where(l => !knownIds.Contains(l.Id)).ToList();
                    if (orphaned.Count > 0)
                    {
                        _logger.LogWarning("Found {Count} device location_id(s) not present in the locations list; adding synthetic entries", orphaned.Count);
                        result = result.Concat(orphaned).ToList().AsReadOnly();
                    }
                }

                _logger.LogInformation("Found {LocationCount} locations after filtering", result.Count);
                foreach (var loc in result)
                {
                    _logger.LogInformation("Location: {LocationId} - {LocationName}", loc.Id, loc.Name);
                }

                _cachedLocations = result;
                _cachedLocationsAt = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations from Ring API: {Message}", ex.Message);
                throw;
            }
            finally
            {
                _locationsCacheLock.Release();
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
        private async Task<IReadOnlyList<Location>> DeriveLocationsFromDevicesAsync(CancellationToken cancellationToken)
        {
            var devices = await GetAllDevicesUnfilteredAsync(cancellationToken);

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
            var allDevices = await GetAllDevicesUnfilteredAsync(cancellationToken);
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

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Device>().AsReadOnly();
                }

                var devices = await session.GetRingDevices();

                var deviceMap = new Dictionary<string, Device>();

                // Add Doorbots
                // Id uses the numeric doorbot id (not the hex device_id) because that's the only
                // identifier the /doorbots/history endpoint's embedded doorbot object populates —
                // history events can't be matched back to a device by device_id (it comes back empty).
                if (devices?.Doorbots != null)
                {
                    foreach (var d in devices.Doorbots)
                    {
                        var deviceId = d.Id.ToString();
                        deviceMap[deviceId] = new Device(
                            Id: deviceId,
                            Name: d.Description ?? "Unknown Device",
                            Type: "doorbot",
                            LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                            IsOnline: d.Subscribed ?? false
                        );
                    }
                }

                // Add Stickup Cameras (skip if already added via Doorbots)
                if (devices?.StickupCams != null)
                {
                    foreach (var d in devices.StickupCams)
                    {
                        var deviceId = d.Id?.ToString() ?? d.DeviceId;
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "stickup_cam",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Subscribed ?? false
                            );
                        }
                    }
                }

                // Add Authorized Doorbots (skip if already added)
                if (devices?.AuthorizedDoorbots != null)
                {
                    foreach (var d in devices.AuthorizedDoorbots)
                    {
                        var deviceId = d.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: d.Description ?? "Unknown Device",
                                Type: "authorized_doorbot",
                                LocationId: d.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: d.Subscribed ?? false
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
                    foreach (var c in devices.Chimes)
                    {
                        var deviceId = c.Id.ToString();
                        if (!deviceMap.ContainsKey(deviceId))
                        {
                            deviceMap[deviceId] = new Device(
                                Id: deviceId,
                                Name: c.Description ?? "Unknown Device",
                                Type: "chime",
                                LocationId: c.LocationId?.ToString() ?? UnknownLocationId,
                                IsOnline: c.Health?.Connected ?? false
                            );
                        }
                    }
                }

                var allAccountDevices = new List<Device>(deviceMap.Values);

                _logger.LogInformation("Found {DeviceCount} devices on the account", allAccountDevices.Count);
                foreach (var device in allAccountDevices)
                {
                    _logger.LogInformation("  Device: {DeviceId} - {DeviceName} ({DeviceType}), Location: {LocationId}, Online: {IsOnline}",
                        device.Id, device.Name, device.Type, device.LocationId, device.IsOnline);
                }

                // Persist device capabilities to database (non-critical, fire-and-forget)
                // TODO: Fix type mismatch between Ring device Ids (string) and Guids
                // _ = PersistDeviceCapabilitiesAsync(allAccountDevices, cancellationToken);

                var readOnlyDevices = allAccountDevices.AsReadOnly();
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
                _allDevicesCacheLock.Release();
            }
        }

        public async Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching device: {DeviceId}", deviceId);

                var locations = await GetLocationsAsync(cancellationToken);

                foreach (var location in locations)
                {
                    var devices = await GetDevicesAsync(location.Id, cancellationToken);
                    var device = devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                        return device;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching device {DeviceId}", deviceId);
                return null;
            }
        }

        // TODO: Fix type mismatch (Ring Device.Id is string, needs Guid conversion)
        // private async Task PersistDeviceCapabilitiesAsync(List<Device> devices, CancellationToken ct)
        // {
        //     if (_capabilitiesRepository == null)
        //         return;
        //
        //     try
        //     {
        //         foreach (var device in devices)
        //         {
        //             // Skip if already persisted for this device
        //             var existing = await _capabilitiesRepository.GetByDeviceIdAsync(device.Id, ct);
        //             if (existing != null)
        //                 continue;
        //
        //             var caps = new VideoForensics.Data.Common.Entities.DeviceCapabilities
        //             {
        //                 Id = Guid.NewGuid(),
        //                 DeviceId = device.Id,
        //                 HasAudio = true,
        //                 HasMotionDetection = true,
        //                 HasCloudStorage = true
        //             };
        //
        //             await _capabilitiesRepository.AddAsync(caps, ct);
        //         }
        //
        //         _logger.LogDebug("Persisted capabilities for {DeviceCount} devices", devices.Count);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogDebug(ex, "Skipping device capabilities persistence (non-critical)");
        //     }
        // }
    }
}
