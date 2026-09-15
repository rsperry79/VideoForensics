using Microsoft.Extensions.Logging;

using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Captures and caches Ring device health telemetry (battery, connectivity, firmware).
    /// Ring only exposes this health data on the device-list response (ring_devices), never on
    /// history/ding events. Capturing it requires a separate fetch, cached to avoid redundant
    /// ring_devices calls when processing multiple devices sequentially.
    /// </summary>
    public interface IRingDeviceHealthCapture
    {
        /// <summary>
        /// Fetches the current device's battery/connectivity telemetry and persists it as a
        /// DeviceHealth metric, so gap-analysis can later explain a recording gap with real data
        /// ("battery was at 8% shortly before this gap began") instead of guessing. Best-effort:
        /// any failure here is logged and swallowed, never fails the underlying video download.
        /// </summary>
        Task CaptureDeviceHealthSnapshotAsync(Session session, string providerDeviceId, Guid deviceGuid, CancellationToken ct);
    }

    public class RingDeviceHealthCapture : IRingDeviceHealthCapture
    {
        private readonly IVideoForensicsDataClient _dataClient;
        private readonly ILogger _logger;

        // Ring only exposes battery/connectivity health on the device-list response (ring_devices),
        // never on history/ding events (see DoorbotHistoryEvent.Doorbot's doc comment) - so capturing
        // it means its own fetch, cached like GetHistoryEventsAsync's cache above so a sequential
        // per-device download loop doesn't re-fetch the whole account's device list for every device.
        private static readonly TimeSpan HealthCacheTtl = TimeSpan.FromSeconds(30);
        private readonly SemaphoreSlim _healthCacheLock = new(1, 1);
        private Devices? _cachedHealthDevices;
        private DateTime _cachedHealthDevicesAt;

        public RingDeviceHealthCapture(IVideoForensicsDataClient dataClient, ILogger logger)
        {
            _dataClient = dataClient ?? throw new ArgumentNullException(nameof(dataClient));
            _logger = logger;
        }

        public async Task CaptureDeviceHealthSnapshotAsync(Session session, string providerDeviceId, Guid deviceGuid, CancellationToken ct)
        {
            try
            {
                Devices? devices = await GetDevicesForHealthAsync(session, ct);
                DeviceHealth? health = DeviceHealthMatcher.FindDeviceHealth(devices, providerDeviceId);
                if (health == null)
                {
                    _logger.LogDebug("No health telemetry available for device {DeviceId} in this run", providerDeviceId);
                    return;
                }

                var deviceHealth = new Data.Common.Entities.DeviceHealth
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceGuid,
                    IsOnline = health.Connected,
                    BatteryPercentage = health.BatteryPercentage.HasValue ? health.BatteryPercentage.Value : null,
                    WifiSignalRssi = health.Rssi.HasValue ? (int)Math.Round(health.Rssi.Value) : null,
                    WifiName = health.WifiName,
                    FirmwareVersion = health.FirmwareVersion,
                    CapturedAtUtc = DateTime.UtcNow
                };

                _ = await _dataClient.RecordDeviceHealthAsync(deviceHealth, ct);
                _logger.LogInformation("Captured health metric for device {DeviceId}: battery={Battery}%, online={IsOnline}, rssi={Rssi}",
                    providerDeviceId, deviceHealth.BatteryPercentage, deviceHealth.IsOnline, deviceHealth.WifiSignalRssi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture device health metric for device {DeviceId} (non-critical)", providerDeviceId);
            }
        }

        private async Task<Devices?> GetDevicesForHealthAsync(Session session, CancellationToken ct)
        {
            if (_cachedHealthDevices != null && DateTime.UtcNow - _cachedHealthDevicesAt < HealthCacheTtl)
            {
                return _cachedHealthDevices;
            }

            await _healthCacheLock.WaitAsync(ct);
            try
            {
                if (_cachedHealthDevices != null && DateTime.UtcNow - _cachedHealthDevicesAt < HealthCacheTtl)
                {
                    return _cachedHealthDevices;
                }

                Devices devices = await session.GetRingDevices();
                _cachedHealthDevices = devices;
                _cachedHealthDevicesAt = DateTime.UtcNow;
                return devices;
            }
            finally
            {
                _ = _healthCacheLock.Release();
            }
        }
    }
}
