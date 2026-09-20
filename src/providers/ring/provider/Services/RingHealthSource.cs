using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;

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
        private readonly IProviderAccountRepository? _providerAccountRepository;
        private readonly IProviderAuthService? _ringAuthService;

        public RingHealthSource(ILogger<RingHealthSource> logger, ISessionProvider sessionProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _providerAccountRepository = null;
            _ringAuthService = null;
        }

        public RingHealthSource(
            ILogger<RingHealthSource> logger,
            ISessionProvider sessionProvider,
            IProviderAccountRepository? providerAccountRepository = null,
            IProviderAuthService? ringAuthService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _providerAccountRepository = providerAccountRepository;
            _ringAuthService = ringAuthService;
        }

        public async Task<IReadOnlyList<DeviceHealthReading>> FetchHealthAsync(CancellationToken ct)
        {
            // If no account repository, fall back to single-session behavior
            if (_providerAccountRepository == null)
            {
                return await FetchHealthForSingleSessionAsync(ct);
            }

            // Multi-account path: fetch health for all active Ring accounts
            var readings = new List<DeviceHealthReading>();

            try
            {
                var accounts = await _providerAccountRepository.ListActiveAsync(ct);
                var ringAccounts = accounts.Where(pa => pa.ProviderName == "Ring").ToList();

                foreach (var account in ringAccounts)
                {
                    try
                    {
                        // Try to get session for this account
                        Session? session = _sessionProvider.GetSession(account.Id);

                        // If no session and auth service available, try to restore
                        if (session == null && _ringAuthService != null)
                        {
                            bool restored = await _ringAuthService.RestoreFromSavedCredentialsAsync(account.Id, ct);
                            if (restored)
                            {
                                session = _sessionProvider.GetSession(account.Id);
                            }
                        }

                        // If still no session, skip this account
                        if (session == null)
                        {
                            _logger.LogDebug("No active or restorable Ring session for account {AccountId}; skipping", account.Id);
                            continue;
                        }

                        // Fetch devices for this account
                        try
                        {
                            Devices devices = await session.GetRingDevices();
                            if (devices != null)
                            {
                                AddReadings(readings, devices.Doorbots, d => d.Id.ToString(), d => d.Health);
                                AddReadings(readings, devices.StickupCams, d => d.Id?.ToString(), d => d.Health);
                                AddReadings(readings, devices.AuthorizedDoorbots, d => d.Id.ToString(), d => d.Health);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch Ring health for account {AccountId} (non-critical)", account.Id);
                            // Continue with next account
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing Ring account {AccountId} (non-critical)", account.Id);
                        // Continue with next account
                    }
                }

                return readings;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Ring device health telemetry for multi-account scenario (non-critical)");
                return Array.Empty<DeviceHealthReading>();
            }
        }

        private async Task<IReadOnlyList<DeviceHealthReading>> FetchHealthForSingleSessionAsync(CancellationToken ct)
        {
            Session? session = _sessionProvider.GetSession();
            if (session == null)
            {
                _logger.LogDebug("No active Ring session; skipping health fetch for this account");
                return Array.Empty<DeviceHealthReading>();
            }

            try
            {
                Devices devices = await session.GetRingDevices();
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

            foreach (T? device in source)
            {
                string? providerDeviceId = idSelector(device);
                DeviceHealth? health = healthSelector(device);
                if (providerDeviceId == null || health == null)
                {
                    continue;
                }

                readings.Add(new DeviceHealthReading(
                    ProviderDeviceId: providerDeviceId,
                    Connected: health.Connected,
                    BatteryPercentage: health.BatteryPercentage.HasValue ? health.BatteryPercentage.Value : null,
                    Rssi: health.Rssi.HasValue ? (int)Math.Round(health.Rssi.Value) : null,
                    WifiName: health.WifiName,
                    FirmwareVersion: health.FirmwareVersion
                ));
            }
        }
    }
}
