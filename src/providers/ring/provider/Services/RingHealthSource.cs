using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Ring's IProviderHealthSource implementation: wraps the same GET /clients_api/ring_devices
    /// call and DeviceHealthMatcher logic RingMediaDownloadService.CaptureDeviceHealthSnapshotAsync
    /// already uses for its per-download-batch snapshot, but returns every device's telemetry from
    /// a single call rather than looking up one device at a time - used by DeviceHealthSyncService's
    /// periodic background sync, not by a download batch.
    /// </summary>
    public class RingHealthSource : IProviderHealthSource
    {
        private readonly ILogger<RingHealthSource> _logger;
        private readonly ISessionProvider _sessionProvider;

        public RingHealthSource(ILogger<RingHealthSource> logger, ISessionProvider sessionProvider)
        {
            _logger = logger;
            _sessionProvider = sessionProvider;
        }

        public async Task<IReadOnlyList<DeviceHealthReading>> FetchHealthAsync(CancellationToken ct)
        {
            var session = _sessionProvider.GetSession();
            if (session == null)
            {
                _logger.LogDebug("No active Ring session; skipping health fetch for this account");
                return Array.Empty<DeviceHealthReading>();
            }

            try
            {
                var devices = await session.GetRingDevices();
                if (devices == null)
                {
                    return Array.Empty<DeviceHealthReading>();
                }

                var readings = new List<DeviceHealthReading>();
                AddReadings(readings, devices.Doorbots, d => d.Id.ToString(), d => d.Health);
                AddReadings(readings, devices.StickupCams, d => d.Id?.ToString(), d => d.Health);
                AddReadings(readings, devices.AuthorizedDoorbots, d => d.Id.ToString(), d => d.Health);

                return readings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Ring device health telemetry (non-critical)");
                return Array.Empty<DeviceHealthReading>();
            }
        }

        private static void AddReadings<T>(
            List<DeviceHealthReading> readings,
            IEnumerable<T>? source,
            Func<T, string?> idSelector,
            Func<T, Entities.DeviceHealth?> healthSelector)
        {
            if (source == null)
            {
                return;
            }

            foreach (var device in source)
            {
                var providerDeviceId = idSelector(device);
                var health = healthSelector(device);
                if (providerDeviceId == null || health == null)
                {
                    continue;
                }

                readings.Add(new DeviceHealthReading(
                    ProviderDeviceId: providerDeviceId,
                    Connected: health.Connected,
                    BatteryPercentage: health.BatteryPercentage.HasValue ? (decimal)health.BatteryPercentage.Value : null,
                    Rssi: health.Rssi.HasValue ? (int)Math.Round(health.Rssi.Value) : null,
                    WifiName: health.WifiName,
                    FirmwareVersion: health.FirmwareVersion
                ));
            }
        }
    }
}
