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

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly SemaphoreSlim _locationsCacheLock = new(1, 1);
        private IReadOnlyList<Location>? _cachedLocations;
        private DateTime _cachedLocationsAt;

        private readonly SemaphoreSlim _devicesCacheLock = new(1, 1);
        private readonly Dictionary<string, (IReadOnlyList<Device> Devices, DateTime FetchedAt)> _cachedDevicesByLocation = new();

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
                    result = await DeriveLocationsFromDevicesAsync(session);
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
        /// Builds a location list from location_id/address fields embedded in the device list
        /// itself (legacy ring_devices endpoint), for accounts where devices/v1/locations comes
        /// back empty. Devices without a location_id are skipped since there's nothing to group
        /// them under.
        /// </summary>
        private async Task<IReadOnlyList<Location>> DeriveLocationsFromDevicesAsync(Session session)
        {
            var devices = await session.GetRingDevices();

            var candidates = Enumerable.Empty<(Guid? LocationId, string Address)>();
            if (devices?.Doorbots != null)
                candidates = candidates.Concat(devices.Doorbots.Select(d => (d.LocationId, d.Address)));
            if (devices?.StickupCams != null)
                candidates = candidates.Concat(devices.StickupCams.Select(d => (d.LocationId, d.Address)));
            if (devices?.AuthorizedDoorbots != null)
                candidates = candidates.Concat(devices.AuthorizedDoorbots.Select(d => (d.LocationId, d.Address)));

            return candidates
                .Where(c => c.LocationId.HasValue)
                .GroupBy(c => c.LocationId!.Value)
                .Select(g => new Location(
                    Id: g.Key.ToString(),
                    Name: g.FirstOrDefault(c => !string.IsNullOrEmpty(c.Address)).Address ?? "Unknown Location",
                    Address: g.FirstOrDefault(c => !string.IsNullOrEmpty(c.Address)).Address
                ))
                .ToList()
                .AsReadOnly();
        }

        public async Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default)
        {
            await _devicesCacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedDevicesByLocation.TryGetValue(locationId, out var cached) &&
                    DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                {
                    _logger.LogInformation("Reusing cached devices for location {LocationId} ({Count}), fetched {Age:F0}s ago",
                        locationId, cached.Devices.Count, (DateTime.UtcNow - cached.FetchedAt).TotalSeconds);
                    return cached.Devices;
                }

                _logger.LogInformation("Fetching devices for location: {LocationId}", locationId);

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new List<Device>().AsReadOnly();
                }

                if (!Guid.TryParse(locationId, out var locId))
                {
                    return new List<Device>().AsReadOnly();
                }

                var devices = await session.GetRingDevices(locId);

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
                            LocationId: d.LocationId?.ToString() ?? locationId,
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
                                LocationId: d.LocationId?.ToString() ?? locationId,
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
                                LocationId: d.LocationId?.ToString() ?? locationId,
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
                                LocationId: c.LocationId?.ToString() ?? locationId,
                                IsOnline: c.Health?.Connected ?? false
                            );
                        }
                    }
                }

                var locationDevices = new List<Device>(deviceMap.Values);

                _logger.LogInformation("Found {DeviceCount} devices in location {LocationId}", locationDevices.Count, locationId);
                foreach (var device in locationDevices)
                {
                    _logger.LogInformation("  Device: {DeviceId} - {DeviceName} ({DeviceType}), Online: {IsOnline}",
                        device.Id, device.Name, device.Type, device.IsOnline);
                }

                // Persist device capabilities to database (non-critical, fire-and-forget)
                // TODO: Fix type mismatch between Ring device Ids (string) and Guids
                // _ = PersistDeviceCapabilitiesAsync(locationDevices, cancellationToken);

                var readOnlyDevices = locationDevices.AsReadOnly();
                _cachedDevicesByLocation[locationId] = (readOnlyDevices, DateTime.UtcNow);
                return readOnlyDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching devices for location {LocationId}", locationId);
                return new List<Device>().AsReadOnly();
            }
            finally
            {
                _devicesCacheLock.Release();
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
