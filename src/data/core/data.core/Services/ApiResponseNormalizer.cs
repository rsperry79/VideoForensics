using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Normalizes Ring API responses to database entities for persistence.</summary>
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

        /// <summary>Normalizes a Ring device from Ring API to Device entity.</summary>
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

            // Mark as synced
            _cacheFreshnessService.MarkSynced(device);
            device.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringDevice);

            _logger.LogDebug("Normalized device {DeviceId} ({DeviceName}) for location {LocationId}",
                device.Id, device.Name, locationId);

            return device;
        }

        /// <summary>Normalizes a Ring location from Ring API to Location entity.</summary>
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

            // Mark as synced
            _cacheFreshnessService.MarkSynced(location);
            location.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringLocation);

            _logger.LogDebug("Normalized location {LocationId} ({LocationName})",
                location.Id, location.Name);

            return location;
        }

        /// <summary>Creates or updates device capabilities from metadata.</summary>
        public DeviceCapabilities CreateDeviceCapabilities(Guid deviceId, IDevice ringDevice)
        {
            var caps = new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                // These would be extracted from ringDevice metadata in a real implementation
                Resolution = ringDevice.MetadataJson?.Contains("1080") == true ? "1080p" : null,
                HasAudio = true, // Ring devices typically have audio
                HasNightVision = true,
                HasMotionDetection = true,
                HasCloudStorage = true,
                MetadataJson = ringDevice.MetadataJson,
            };

            _cacheFreshnessService.MarkSynced(caps);
            caps.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringDevice);

            return caps;
        }

        /// <summary>Creates or updates device health from metadata snapshot.</summary>
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

        /// <summary>Creates location metadata from Ring location data.</summary>
        public LocationMetadata CreateLocationMetadata(Guid locationId, ILocation ringLocation)
        {
            var metadata = new LocationMetadata
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                StreetAddress = ringLocation.Address,
                // Additional address components would be extracted from ringLocation.MetadataJson in real implementation
                TimeZoneId = null, // Would be extracted from Ring API
                MetadataJson = ringLocation.MetadataJson,
            };

            _cacheFreshnessService.MarkSynced(metadata);
            metadata.ApiResponseHash = _cacheFreshnessService.ComputeHash(ringLocation);

            return metadata;
        }
    }
}
