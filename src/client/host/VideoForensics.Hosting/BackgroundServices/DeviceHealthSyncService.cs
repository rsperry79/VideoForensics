using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Periodically polls every registered IProviderHealthSource (Ring today; more providers later,
    /// registered the same way) and persists a DeviceHealth metric per device via the same
    /// IVideoForensicsDataClient.RecordDeviceHealthAsync entrypoint the per-download-batch
    /// capture now uses - one persistence path, not two. This is what makes jamming detection
    /// (JammingToolsOrchestrator) have RSSI history to analyze even between actual video downloads,
    /// see the plan's §3.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.WebApp) - never MAUI: a
    /// health source calls a provider's API directly, and per §1's "only the server pulls from any
    /// provider" rule, no client host may do that.
    ///
    /// Implements tiered polling: base interval (15min) plus elevated polling when RSSI degradation
    /// is detected, running a faster check to determine if jamming occurred (plan §4.2).
    /// </summary>
    public class DeviceHealthSyncService : BackgroundService
    {
        private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(15);
        private const int OnBatteryIntervalMultiplier = 3;
        // DegradationThresholdDb matches JammingToolsOrchestrator's threshold
        private const double DegradationThresholdDb = 8.0;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly IBatteryStatusProvider _batteryStatusProvider;
        private readonly ILogger<DeviceHealthSyncService> _logger;
        private readonly ElevatedPollingWindowTracker _elevatedTracker;

        public DeviceHealthSyncService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            IBatteryStatusProvider batteryStatusProvider,
            ILogger<DeviceHealthSyncService> logger,
            ElevatedPollingWindowTracker elevatedTracker)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _batteryStatusProvider = batteryStatusProvider;
            _logger = logger;
            _elevatedTracker = elevatedTracker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run base and elevated polling loops in parallel as independent tasks
            await Task.WhenAll(
                RunBaseLoopAsync(stoppingToken),
                RunElevatedLoopAsync(stoppingToken));
        }

        private int _tickCount;

        /// <summary>
        /// Base polling loop: runs every 15 minutes (or 45 minutes on battery).
        /// Detects RSSI degradation and enters elevated windows as needed.
        /// </summary>
        private async Task RunBaseLoopAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(BaseInterval);

            do
            {
                if (!_config.EnableHealthSync)
                {
                    _logger.LogDebug("Health sync is disabled via configuration; skipping this tick");
                    continue;
                }

                bool onBattery = _batteryStatusProvider.GetStatus() == BatteryStatus.OnBattery;
                if (onBattery)
                {
                    // Skip most ticks while on battery rather than running a separate slower timer,
                    // so the effective interval is roughly BaseInterval * OnBatteryIntervalMultiplier
                    // without restarting the PeriodicTimer.
                    bool shouldRun = System.Threading.Interlocked.Increment(ref _tickCount) % OnBatteryIntervalMultiplier == 0;
                    if (!shouldRun)
                    {
                        continue;
                    }
                }

                await RunOneTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>
        /// Elevated polling loop: runs faster (every 45 seconds by default) when devices are in elevated windows.
        /// Runs jamming analysis and manages live view sessions.
        /// </summary>
        private async Task RunElevatedLoopAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_config.ElevatedPollingIntervalSeconds));

            do
            {
                if (!_config.EnableHealthSync)
                {
                    _logger.LogDebug("Elevated polling is disabled via configuration; skipping this tick");
                    continue;
                }

                await RunElevatedTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Internal for direct testability (VideoForensics.Hosting.Tests) without driving the whole BackgroundService lifecycle/timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            var healthSources = sp.GetServices<IProviderHealthSource>().ToList();
            if (healthSources.Count == 0)
            {
                return;
            }

            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IVideoForensicsDataClient dataClient = sp.GetRequiredService<IVideoForensicsDataClient>();
            IDeviceHealthRepository healthRepository = sp.GetRequiredService<IDeviceHealthRepository>();

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

            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            ISecurityAuditLogger auditLog = sp.GetRequiredService<ISecurityAuditLogger>();

            foreach (IProviderHealthSource? healthSource in healthSources)
            {
                string providerName = healthSource.GetType().Name;

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
                    IReadOnlyList<DeviceHealthReading> readings = await healthSource.FetchHealthAsync(ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    int persisted = 0;

                    foreach (DeviceHealthReading reading in readings)
                    {
                        if (!devicesByProviderId.TryGetValue(reading.ProviderDeviceId, out Data.Common.Entities.Device? device))
                        {
                            continue;
                        }

                        var health = new DeviceHealth
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            IsOnline = reading.Connected,
                            BatteryPercentage = reading.BatteryPercentage,
                            WifiSignalRssi = reading.Rssi,
                            WifiName = reading.WifiName,
                            FirmwareVersion = reading.FirmwareVersion,
                            CapturedAtUtc = DateTime.UtcNow
                        };

                        _ = await dataClient.RecordDeviceHealthAsync(health, ct);
                        persisted++;

                        // Check for RSSI degradation and trigger elevated polling if detected (non-critical check).
                        try
                        {
                            if (health.WifiSignalRssi.HasValue)
                            {
                                // Fetch a small recent window of readings for this device to compute baseline.
                                IReadOnlyList<DeviceHealth> history = await healthRepository.GetHistoryAsync(device.Id, ct);
                                if (history.Count >= 3)
                                {
                                    // Use the last few readings (excluding the one we just persisted) to compute baseline.
                                    var recentReadings = history
                                        .Where(h => h.WifiSignalRssi.HasValue)
                                        .Take(5)
                                        .Select(h => (double)h.WifiSignalRssi!.Value)
                                        .ToList();

                                    if (recentReadings.Count >= 3)
                                    {
                                        double medianRssi = Median(recentReadings);
                                        double degradation = medianRssi - health.WifiSignalRssi.Value;

                                        if (degradation >= DegradationThresholdDb)
                                        {
                                            _elevatedTracker.EnterElevated(
                                                device.Id,
                                                TimeSpan.FromMinutes(_config.ElevatedPollingWindowMinutes));
                                            _logger.LogInformation(
                                                "Health sync tick: device {DeviceId} RSSI degraded by {Degradation:F1} dB; entering elevated polling window",
                                                device.Id, degradation);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Non-critical: don't let degradation check failure break the main tick
                            _logger.LogWarning(ex, "Health sync tick: degradation check for device {DeviceId} failed (non-critical)", device.Id);
                        }
                    }

                    _logger.LogInformation(
                        "Health sync tick: {SourceType} returned {ReadingCount} reading(s), persisted {PersistedCount} metric(s)",
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

        /// <summary>Internal for direct testability; runs one tick of elevated polling for devices in elevated windows.</summary>
        internal async Task RunElevatedTickAsync(CancellationToken ct)
        {
            IReadOnlyCollection<Guid> elevatedDevices = _elevatedTracker.ElevatedDeviceIds;
            if (elevatedDevices.Count == 0)
            {
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            var healthSources = sp.GetServices<IProviderHealthSource>().ToList();
            if (healthSources.Count == 0)
            {
                return;
            }

            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IVideoForensicsDataClient dataClient = sp.GetRequiredService<IVideoForensicsDataClient>();
            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            ISecurityAuditLogger auditLog = sp.GetRequiredService<ISecurityAuditLogger>();

            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devices;
            try
            {
                devices = await deviceRepository.ListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Elevated polling tick: failed to list devices; skipping this tick");
                return;
            }

            var devicesByProviderId = devices
                .GroupBy(d => d.ProviderDeviceId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (IProviderHealthSource? healthSource in healthSources)
            {
                string providerName = healthSource.GetType().Name;

                // Budget-guard the elevated tick's provider calls like the base tick does.
                if (!await budgetGuard.TryConsumeAsync(providerName, ct))
                {
                    _logger.LogWarning("Elevated polling tick: skipping {ProviderName} - provider API budget exceeded for this window", providerName);
                    continue;
                }

                try
                {
                    IReadOnlyList<DeviceHealthReading> readings = await healthSource.FetchHealthAsync(ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    int persisted = 0;

                    foreach (DeviceHealthReading reading in readings)
                    {
                        if (!devicesByProviderId.TryGetValue(reading.ProviderDeviceId, out Data.Common.Entities.Device? device))
                        {
                            continue;
                        }

                        // Only persist readings for devices in elevated windows.
                        if (!elevatedDevices.Contains(device.Id))
                        {
                            continue;
                        }

                        var health = new DeviceHealth
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            IsOnline = reading.Connected,
                            BatteryPercentage = reading.BatteryPercentage,
                            WifiSignalRssi = reading.Rssi,
                            WifiName = reading.WifiName,
                            FirmwareVersion = reading.FirmwareVersion,
                            CapturedAtUtc = DateTime.UtcNow
                        };

                        _ = await dataClient.RecordDeviceHealthAsync(health, ct);
                        persisted++;
                    }

                    if (persisted > 0)
                    {
                        _logger.LogInformation(
                            "Elevated polling tick: {SourceType} persisted {PersistedCount} metric(s) for elevated device(s)",
                            healthSource.GetType().Name, persisted);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Elevated polling tick: {SourceType} failed (non-critical)", healthSource.GetType().Name);
                }
            }

            // Now run jamming analysis and manage live view sessions for each elevated device.
            var jammingOrchestrator = sp.GetRequiredService<JammingToolsOrchestrator>();
            var liveViewService = sp.GetRequiredService<ILiveViewSessionService>();

            foreach (Guid deviceId in elevatedDevices.ToList())
            {
                try
                {
                    // Analyze the last hour for jamming incidents.
                    JammingAnalysisReport analysis = await jammingOrchestrator.AnalyzeJammingAsync(
                        deviceId,
                        DateTime.UtcNow.AddHours(-1),
                        DateTime.UtcNow,
                        ct);

                    if (analysis.NewlyDetectedCount > 0)
                    {
                        // Jamming confirmed: start or promote live view session.
                        LiveViewSession? activeSession = await liveViewService.GetActiveSessionAsync(deviceId, ct);
                        if (activeSession == null)
                        {
                            // No active session: start one with JammingConfirmed trigger.
                            _ = await liveViewService.StartAsync(deviceId, LiveViewTriggerReason.JammingConfirmed, null, ct);
                            _logger.LogInformation(
                                "Elevated polling tick: jamming confirmed for device {DeviceId}; started new live view session",
                                deviceId);
                        }
                        else
                        {
                            // Active session exists: promote to sustained mode.
                            _ = await liveViewService.PromoteToSustainedAsync(activeSession.Id, "Jamming confirmed during elevated polling window", ct);
                            _logger.LogInformation(
                                "Elevated polling tick: jamming confirmed for device {DeviceId}; promoted session {SessionId} to sustained mode",
                                deviceId, activeSession.Id);
                        }
                    }

                    // Clear the elevated window regardless of confirmation (window's job is done once re-checked).
                    _elevatedTracker.Clear(deviceId);
                }
                catch (Exception ex)
                {
                    // Isolate per-device failures: one device's issue doesn't break others.
                    _logger.LogWarning(ex, "Elevated polling tick: jamming analysis/live view management failed for device {DeviceId} (non-critical)", deviceId);
                }
            }
        }

        /// <summary>Computes the median of a collection of doubles.</summary>
        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }
}
