using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Data.Core.Services
{
    public class ApiResponseNormalizer
    {
        private readonly ILogger<ApiResponseNormalizer> _logger;
        private readonly CacheFreshnessService _cacheFreshnessService;

        public ApiResponseNormalizer(
            ILogger<ApiResponseNormalizer> logger,
            CacheFreshnessService cacheFreshnessService)
        {
            _logger = logger;
            _cacheFreshnessService = cacheFreshnessService;
        }

        public Device NormalizeDevice(
            IDevice ringDevice,
            Guid locationId,
            string? timeZoneId = null)
        {
            var device = new Device
            {
                Id = ringDevice.Id,
                LocationId = locationId,
                ProviderDeviceId = ringDevice.ProviderDeviceId ?? "",
                Name = ringDevice.Name ?? "Unknown",
                Type = ringDevice.Type ?? "unknown",
                IsOnline = ringDevice.IsOnline,
                TimeZoneId = timeZoneId,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(ringDevice),
            };

            _cacheFreshnessService.MarkSynced(device);
            device.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringDevice);
            _logger.LogDebug("Normalized device {DeviceId}", device.Id);
            return device;
        }

        public Location NormalizeLocation(
            ILocation ringLocation,
            Guid providerAccountId)
        {
            var location = new Location
            {
                Id = ringLocation.Id,
                ProviderAccountId = providerAccountId,
                ProviderLocationId = ringLocation.ProviderLocationId ?? "",
                Name = ringLocation.Name ?? "Unknown",
                Address = ringLocation.Address,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(ringLocation),
            };

            _cacheFreshnessService.MarkSynced(location);
            location.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringLocation);
            _logger.LogDebug("Normalized location {LocationId}", location.Id);
            return location;
        }

        public DeviceCapabilities CreateDeviceCapabilities(Guid deviceId, IDevice ringDevice)
        {
            var caps = new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Resolution = "1080p",
                HasAudio = true,
                HasNightVision = true,
                HasMotionDetection = true,
                HasCloudStorage = true,
                MetadataJson = ringDevice.MetadataJson,
            };

            _cacheFreshnessService.MarkSynced(caps);
            caps.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringDevice);
            return caps;
        }

        public DeviceHealth CreateDeviceHealth(
            Guid deviceId,
            int? batteryPct = null,
            int? rssi = null,
            string? wifiName = null,
            bool? isOnline = null)
        {
            var health = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                BatteryPercentage = batteryPct.HasValue ? (decimal)batteryPct.Value : null,
                WifiSignalRssi = rssi,
                WifiName = wifiName,
                IsOnline = isOnline,
                LastHeartbeatUtc = DateTime.UtcNow,
                Status = isOnline == true ? "online" : "offline",
            };

            _cacheFreshnessService.MarkSynced(health);
            return health;
        }

        public LocationMetadata CreateLocationMetadata(Guid locationId, ILocation ringLocation)
        {
            var metadata = new LocationMetadata
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                StreetAddress = ringLocation.Address,
                TimeZoneId = null,
                MetadataJson = ringLocation.MetadataJson,
            };

            _cacheFreshnessService.MarkSynced(metadata);
            metadata.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringLocation);
            return metadata;
        }
    }
}
