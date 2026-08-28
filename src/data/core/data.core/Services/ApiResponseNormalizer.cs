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
    }
}
