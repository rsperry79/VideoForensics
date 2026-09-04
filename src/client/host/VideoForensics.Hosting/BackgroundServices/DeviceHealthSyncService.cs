using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Periodically polls every registered IProviderHealthSource (Ring today; more providers later,
    /// registered the same way) and persists a DeviceHealthSnapshot per device via the same
    /// IVideoForensicsDataClient.RecordDeviceHealthSnapshotAsync entrypoint the per-download-batch
    /// capture already uses - one persistence path, not two. This is what makes jamming detection
    /// (JammingToolsOrchestrator) have RSSI history to analyze even between actual video downloads,
    /// see the plan's §3.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.WebApp) - never MAUI: a
    /// health source calls a provider's API directly, and per §1's "only the server pulls from any
    /// provider" rule, no client host may do that.
    /// </summary>
    public class DeviceHealthSyncService : BackgroundService
    {
        private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(15);
        private const int OnBatteryIntervalMultiplier = 3;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly IBatteryStatusProvider _batteryStatusProvider;
        private readonly ILogger<DeviceHealthSyncService> _logger;

        public DeviceHealthSyncService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            IBatteryStatusProvider batteryStatusProvider,
            ILogger<DeviceHealthSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _batteryStatusProvider = batteryStatusProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(BaseInterval);

            do
            {
                if (!_config.EnableHealthSync)
                {
                    _logger.LogDebug("Health sync is disabled via configuration; skipping this tick");
                    continue;
                }

                var onBattery = _batteryStatusProvider.GetStatus() == BatteryStatus.OnBattery;
                if (onBattery)
                {
                    // Skip most ticks while on battery rather than running a separate slower timer,
                    // so the effective interval is roughly BaseInterval * OnBatteryIntervalMultiplier
                    // without restarting the PeriodicTimer.
                    var shouldRun = System.Threading.Interlocked.Increment(ref _tickCount) % OnBatteryIntervalMultiplier == 0;
                    if (!shouldRun)
                    {
                        continue;
                    }
                }

                await RunOneTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private int _tickCount;

        /// <summary>Internal for direct testability (VideoForensics.Hosting.Tests) without driving the whole BackgroundService lifecycle/timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var healthSources = sp.GetServices<IProviderHealthSource>().ToList();
            if (healthSources.Count == 0)
            {
                return;
            }

            var deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            var dataClient = sp.GetRequiredService<IVideoForensicsDataClient>();

            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devices;
            try
            {
                devices = await deviceRepository.ListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health sync tick: failed to list devices; skipping this tick");
                return;
            }

            if (devices.Count == 0)
            {
                return;
            }

            var devicesByProviderId = devices
                .GroupBy(d => d.ProviderDeviceId)
                .ToDictionary(g => g.Key, g => g.First());

            var budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            var auditLog = sp.GetRequiredService<ISecurityAuditLogger>();

            foreach (var healthSource in healthSources)
            {
                var providerName = healthSource.GetType().Name;

                // Provider API budget guard (plan §5.12): check BEFORE calling out to the provider,
                // so a blown budget shows up as an explicit "skipped this tick" rather than a
                // silent absence that a viewer could mistake for "confirmed no incidents".
                if (!await budgetGuard.TryConsumeAsync(providerName, ct))
                {
                    _logger.LogWarning("Health sync tick: skipping {ProviderName} - provider API budget exceeded for this window", providerName);
                    continue;
                }

                try
                {
                    var readings = await healthSource.FetchHealthAsync(ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    var persisted = 0;

                    foreach (var reading in readings)
                    {
                        if (!devicesByProviderId.TryGetValue(reading.ProviderDeviceId, out var device))
                        {
                            continue;
                        }

                        var snapshot = new DeviceHealthSnapshot
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            Connected = reading.Connected,
                            BatteryPercentage = reading.BatteryPercentage,
                            Rssi = reading.Rssi,
                            WifiName = reading.WifiName,
                            FirmwareVersion = reading.FirmwareVersion,
                            CapturedAtUtc = DateTime.UtcNow
                        };

                        await dataClient.RecordDeviceHealthSnapshotAsync(snapshot, ct);
                        persisted++;
                    }

                    _logger.LogInformation(
                        "Health sync tick: {SourceType} returned {ReadingCount} reading(s), persisted {PersistedCount} snapshot(s)",
                        healthSource.GetType().Name, readings.Count, persisted);
                }
                catch (Exception ex)
                {
                    // One provider's failure must not stop another's - each health source gets its
                    // own try/catch, matching the plan's explicit call-out for this.
                    _logger.LogWarning(ex, "Health sync tick: {SourceType} failed (non-critical)", healthSource.GetType().Name);
                }
            }
        }
    }
}
