using Microsoft.Extensions.Logging;

using System.Collections.ObjectModel;

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

                Session? session = _sessionProvider.GetSession();
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

                Session? session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Device>().AsReadOnly();
                }

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
                    foreach (Entities.StickupCam d in devices.StickupCams)
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
                    foreach (Entities.Doorbot d in devices.AuthorizedDoorbots)
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
                    foreach (Entities.Chime c in devices.Chimes)
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
                foreach (Device device in allAccountDevices)
                {
                    _logger.LogInformation("  Device: {DeviceId} - {DeviceName} ({DeviceType}), Location: {LocationId}, Online: {IsOnline}",
                        device.Id, device.Name, device.Type, device.LocationId, device.IsOnline);
                }

                // Persist device capabilities to database (non-critical, fire-and-forget)
                _ = Task.Run(() => PersistDeviceCapabilitiesAsync(allAccountDevices, cancellationToken), cancellationToken);

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

        /// <summary>
        /// Captures and persists location metadata as an audit trail of location discovery.
        /// Runs as a fire-and-forget background task for forensic completeness.
        /// </summary>
        private async Task PersistLocationMetadataAsync(IReadOnlyList<Location> locations, CancellationToken ct)
        {
            if (_metadataRepository == null)
            {
                _logger.LogDebug("Location metadata repository not available; skipping metadata capture");
                return;
            }

            try
            {
                foreach (var location in locations)
                {
                    if (string.IsNullOrEmpty(location.Id))
                    {
                        _logger.LogWarning("Location has no ID; skipping metadata capture for this location");
                        continue;
                    }

                    // Convert location string ID to deterministic Guid
                    Guid locationGuid = GuidFromString(location.Id);

                    // Check if metadata already captured for this discovery cycle
                    var existing = await _metadataRepository.GetByLocationIdAsync(locationGuid, ct);
                    if (existing != null && existing.CapturedAtUtc > DateTime.UtcNow.AddMinutes(-5))
                    {
                        _logger.LogDebug("Location metadata recently captured for location {LocationId}", location.Id);
                        continue;
                    }

                    var metadata = new VideoForensics.Data.Common.Entities.LocationMetadata
                    {
                        Id = Guid.NewGuid(),
                        LocationId = locationGuid,
                        Name = location.Name,
                        Address = location.Address,
                        ProviderLocationId = location.Id,
                        CapturedAtUtc = DateTime.UtcNow,
                        Source = "Ring API Discovery"
                    };

                    await _metadataRepository.AddAsync(metadata, ct);
                    _logger.LogDebug("Captured metadata for location {LocationId} ({LocationName})", location.Id, location.Name);
                }

                _logger.LogDebug("Location metadata capture completed for {LocationCount} location(s)", locations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping location metadata capture (non-critical operation failed)");
            }
        }

        /// <summary>
        /// Persists device capabilities for all devices discovered from Ring API.
        /// Runs as a fire-and-forget background task since it's non-critical infrastructure.
        /// Note: Ring device IDs are strings; we use a deterministic Guid hash for persistence.
        /// </summary>
        private async Task PersistDeviceCapabilitiesAsync(List<Device> devices, CancellationToken ct)
        {
            if (_capabilitiesRepository == null)
            {
                _logger.LogDebug("Device capabilities repository not available; skipping persistence");
                return;
            }

            try
            {
                foreach (var device in devices)
                {
                    if (string.IsNullOrEmpty(device.Id))
                    {
                        _logger.LogWarning("Device has no ID; skipping capability persistence for this device");
                        continue;
                    }

                    // Convert Ring string device ID to a deterministic Guid using namespace hashing
                    // This ensures the same device ID always maps to the same Guid across runs
                    Guid deviceGuid = GuidFromString(device.Id);

                    // Skip if already persisted for this device
                    var existing = await _capabilitiesRepository.GetByDeviceIdAsync(deviceGuid, ct);
                    if (existing != null)
                    {
                        _logger.LogDebug("Device capabilities already persisted for device {DeviceId}", device.Id);
                        continue;
                    }

                    var caps = new VideoForensics.Data.Common.Entities.DeviceCapabilities
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceGuid,
                        HasAudio = true,
                        HasMotionDetection = true,
                        HasCloudStorage = true
                    };

                    await _capabilitiesRepository.AddAsync(caps, ct);
                    _logger.LogDebug("Persisted capabilities for device {DeviceId} ({DeviceName})", device.Id, device.Name);
                }

                _logger.LogDebug("Device capabilities persistence completed for {DeviceCount} device(s)", devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping device capabilities persistence (non-critical operation failed)");
            }
        }

        /// <summary>
        /// Converts a string device ID to a deterministic Guid using namespace-based GUID hashing.
        /// This ensures consistent Guid generation for the same string across multiple runs.
        /// </summary>
        private static Guid GuidFromString(string deviceId)
        {
            // Use a fixed namespace GUID for Ring device IDs
            var ringNamespace = new Guid("3d84d9b5-91d4-4c7f-b6a9-2e7c8f3d1e9a");

            // Create version 5 (SHA-1) GUID from namespace and device ID
            var bytes = System.Text.Encoding.UTF8.GetBytes(deviceId);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hash = sha1.ComputeHash(bytes);
                var guidBytes = new byte[16];
                Array.Copy(hash, guidBytes, 16);

                // Set version to 5 (SHA-1 based)
                guidBytes[6] = (byte)((guidBytes[6] & 0x0f) | 0x50);
                guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);

                return new Guid(guidBytes);
            }
        }
    }
}
