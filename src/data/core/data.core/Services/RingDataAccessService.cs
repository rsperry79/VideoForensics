using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Orchestrates GetOrFetch calls for Ring API data with configurable TTLs.</summary>
    public class RingDataAccessService
    {
        private readonly ILogger<RingDataAccessService> _logger;
        private readonly CacheFreshnessService _cacheFreshnessService;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;

        // Default cache TTLs in minutes
        private const int AccountTtlMinutes = 1440; // 24 hours
        private const int DeviceTtlMinutes = 360; // 6 hours
        private const int LocationTtlMinutes = 1440; // 24 hours
        private const int EventTtlMinutes = 0; // Never cache events (always fresh)

        public RingDataAccessService(
            ILogger<RingDataAccessService> logger,
            CacheFreshnessService cacheFreshnessService,
            IDeviceRepository deviceRepository,
            ILocationRepository locationRepository,
            IProviderAccountRepository providerAccountRepository)
        {
            _logger = logger;
            _cacheFreshnessService = cacheFreshnessService;
            _deviceRepository = deviceRepository;
            _locationRepository = locationRepository;
            _providerAccountRepository = providerAccountRepository;
        }

        /// <summary>Gets a device from cache if fresh, indicates if API fetch needed.</summary>
        public async Task<(Device? device, bool needsApiFetch)> GetOrCheckDeviceAsync(
            Guid deviceId, CancellationToken ct)
        {
            var cached = await _deviceRepository.GetAsync(deviceId, ct);
            if (cached == null)
            {
                return (null, true); // Not in cache, need API
            }

            if (_cacheFreshnessService.IsStale(cached, DeviceTtlMinutes))
            {
                _logger.LogInformation("Device cache stale for {DeviceId}, will refetch from API", deviceId);
                return (cached, true); // Cache stale, need API
            }

            _logger.LogDebug("Device cache hit for {DeviceId}, age {AgeMinutes}min",
                deviceId, _cacheFreshnessService.GetAgeMinutes(cached));
            return (cached, false); // Cache fresh
        }

        /// <summary>Gets a location from cache if fresh, indicates if API fetch needed.</summary>
        public async Task<(Location? location, bool needsApiFetch)> GetOrCheckLocationAsync(
            Guid locationId, CancellationToken ct)
        {
            var cached = await _locationRepository.GetAsync(locationId, ct);
            if (cached == null)
            {
                return (null, true); // Not in cache, need API
            }

            if (_cacheFreshnessService.IsStale(cached, LocationTtlMinutes))
            {
                _logger.LogInformation("Location cache stale for {LocationId}, will refetch from API", locationId);
                return (cached, true); // Cache stale, need API
            }

            _logger.LogDebug("Location cache hit for {LocationId}, age {AgeMinutes}min",
                locationId, _cacheFreshnessService.GetAgeMinutes(cached));
            return (cached, false); // Cache fresh
        }

        /// <summary>Persists device to cache and marks as synced.</summary>
        public async Task<Device> PersistDeviceAsync(Device device, CancellationToken ct)
        {
            _cacheFreshnessService.MarkSynced(device);
            await _deviceRepository.UpdateAsync(device, ct);
            _logger.LogInformation("Persisted device {DeviceId} to cache", device.Id);
            return device;
        }

        /// <summary>Persists location to cache and marks as synced.</summary>
        public async Task<Location> PersistLocationAsync(Location location, CancellationToken ct)
        {
            _cacheFreshnessService.MarkSynced(location);
            await _locationRepository.UpdateAsync(location, ct);
            _logger.LogInformation("Persisted location {LocationId} to cache", location.Id);
            return location;
        }

        /// <summary>Events are always fresh - never cached. This logs the fetch intent.</summary>
        public void LogEventFetch(Guid deviceId, DateTime fromUtc, DateTime toUtc)
        {
            _logger.LogInformation(
                "Fetching fresh events for device {DeviceId} from {FromUtc} to {ToUtc} (never cached)",
                deviceId, fromUtc, toUtc);
        }

        /// <summary>Logs when cache prevents an API call.</summary>
        public void LogCacheHit<T>(Guid id, int ageMinutes)
        {
            _logger.LogInformation("Cache hit for {Type} {Id}, saved API call (age {AgeMinutes}min)",
                typeof(T).Name, id, ageMinutes);
        }

        /// <summary>Logs when cache miss forces an API call.</summary>
        public void LogCacheMiss<T>(Guid id)
        {
            _logger.LogInformation("Cache miss for {Type} {Id}, fetching from API", typeof(T).Name, id);
        }
    }
}
