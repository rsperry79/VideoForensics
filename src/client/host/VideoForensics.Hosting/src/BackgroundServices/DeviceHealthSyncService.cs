using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Periodically polls every registered IProviderHealthSource (Ring today; more providers later,
    /// registered the same way) and persists a DeviceHealth metric per device via the same
    /// IVideoForensicsDataClient.RecordDeviceHealthAsync entrypoint the per-download-batch
    /// capture now uses - one persistence path, not two. This is what makes jamming detection
    /// (JammingToolsOrchestrator) have RSSI history to analyze even between actual video downloads,
    /// see the plan's §3.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.Client.WebApp) - never MAUI: a
    /// health source calls a provider's API directly, and per §1's "only the server pulls from any
    /// provider" rule, no client host may do that.
    ///
    /// Implements tiered polling: base interval (DeviceHealthIntervalMinutes, 15 by default) plus elevated polling when RSSI degradation
    /// is detected, running a faster check to determine if jamming occurred (plan §4.2).
    /// </summary>
    public class DeviceHealthSyncService : BackgroundService
    {
        private const int OnBatteryIntervalMultiplier = 3;
        // DegradationThresholdDb matches JammingToolsOrchestrator's threshold
        private const double DegradationThresholdDb = 8.0;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly IBatteryStatusProvider _batteryStatusProvider;
        private readonly ILogger<DeviceHealthSyncService> _logger;
        private readonly ElevatedPollingWindowTracker _elevatedTracker;

        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>The base wait, read from the Services page setting before each wait.</summary>
        internal TimeSpan CurrentInterval => TimeSpan.FromMinutes(_config.DeviceHealthIntervalMinutes);

        public DeviceHealthSyncService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            IBatteryStatusProvider batteryStatusProvider,
            ILogger<DeviceHealthSyncService> logger,
            ElevatedPollingWindowTracker elevatedTracker, ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _heartbeats = heartbeats;
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
        /// Base polling loop: runs every DeviceHealthIntervalMinutes (or three times that on battery).
        /// Detects RSSI degradation and enters elevated windows as needed.
        /// </summary>
        private async Task RunBaseLoopAsync(CancellationToken stoppingToken)
        {
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
                    // Skip most ticks while on battery rather than running a separate slower wait,
                    // so the effective interval is roughly CurrentInterval * OnBatteryIntervalMultiplier.
                    bool shouldRun = System.Threading.Interlocked.Increment(ref _tickCount) % OnBatteryIntervalMultiplier == 0;
                    if (!shouldRun)
                    {
                        continue;
                    }
                }

                _heartbeats?.Beat(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(45));
                await RunOneTickAsync(stoppingToken);
            }
            while (await WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>
        /// Waits the configured base interval, read now so a changed setting applies from the next wait. A fresh delay each
        /// tick is used instead of a PeriodicTimer, which is fixed at creation and would ignore the change.
        /// </summary>
        private async Task<bool> WaitForNextTickAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(CurrentInterval, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
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
                if (!_config.EnableElevatedPolling)
                {
                    _logger.LogDebug("Elevated polling is off (EnableElevatedPolling); skipping this tick");
                    continue;
                }

                await RunElevatedTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Internal for direct testability (VideoForensics.Client.Hosting.Tests) without driving the whole BackgroundService lifecycle/timer.</summary>
        /// <summary>
        /// Runs the signal-deviation check for one camera. A failure is logged and does not stop the poll: the reading
        /// is already saved, and the next poll checks again.
        /// </summary>
        private async Task<bool> CheckSignalDeviationAsync(IServiceProvider sp, VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            try
            {
                _ = await sp.GetRequiredService<SignalDeviationAlerts>().CheckAsync(device, ct);
                return true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Signal deviation check failed for {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                return false;
            }
        }

        /// <summary>
        /// Runs the bit rate deviation check for one camera. A failure is logged and does not stop the poll: the reading
        /// is already saved, and the next poll checks again.
        /// </summary>
        private async Task<bool> CheckBitrateDeviationAsync(IServiceProvider sp, VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            try
            {
                _ = await sp.GetRequiredService<BitrateDeviationAlerts>().CheckAsync(device, ct);
                return true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Bit rate deviation check failed for {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                return false;
            }
        }

        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;
            using IDisposable? lease = sp.TryEnter();
            if (lease is null)
            {
                return;
            }

            var healthSources = sp.GetServices<IProviderHealthSource>().ToList();
            if (healthSources.Count == 0)
            {
                return;
            }

            bool itemFailed = false;
            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IVideoForensicsDataClient dataClient = sp.GetRequiredService<IVideoForensicsDataClient>();
            IDeviceHealthRepository healthRepository = sp.GetRequiredService<IDeviceHealthRepository>();
            INotificationDispatcher? dispatcher = sp.GetService<INotificationDispatcher>();

            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devices;
            try
            {
                devices = await deviceRepository.ListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Health sync tick: failed to list devices; skipping this tick: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed");
                return;
            }

            if (devices.Count == 0)
            {
                if (itemFailed) { _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed"); } else { _heartbeats?.RecordRun(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow); }
                return;
            }

            // A camera with snapshot capture on gets its health from each capture (SnapshotSyncBackgroundService), so this poll leaves
            // it out. Cameras with capture off, chimes and other non-cameras are still polled.
            var devicesByProviderId = devices
                .Where(d => !(d.SnapshotCaptureEnabled && SnapshotSyncBackgroundService.HasCamera(d.Type)))
                .GroupBy(d => d.ProviderDeviceId)
                .ToDictionary(g => g.Key, g => g.First());
            if (devicesByProviderId.Count == 0)
            {
                if (itemFailed) { _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed"); } else { _heartbeats?.RecordRun(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow); }
                return;
            }

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
                    _logger.LogDebug("Health sync tick: skipping {ProviderName} - provider API budget exceeded for this window", providerName);
                    continue;
                }

                try
                {
                    IReadOnlyList<DeviceHealthReading> readings = await healthSource.FetchHealthAsync(ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    int persisted = 0;

                    foreach (DeviceHealthReading reading in readings)
                    {
                        if (!devicesByProviderId.TryGetValue(reading.ProviderDeviceId, out Data.Common.Entities.Device? device) || !device.HealthCaptureEnabled)
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
                            BatteryVoltageValue = reading.BatteryVoltageValue,
                            IsExternalPowerConnected = reading.IsExternalPowerConnected,
                            OtaStatus = reading.OtaStatus,
                            LastHeartbeatUtc = reading.LastHeartbeatUtc,
                            CapturedAtUtc = DateTime.UtcNow,
                            DeviceType = reading.DeviceType,
                            FirmwareVersionStatus = reading.FirmwareVersionStatus,
                            BatteryPresent = reading.BatteryPresent,
                            BatteryPercentageCategory = reading.BatteryPercentageCategory,
                            BatteryVoltageCategory = reading.BatteryVoltageCategory,
                            RssiCategory = reading.RssiCategory,
                            NetworkConnectionValue = reading.NetworkConnectionValue,
                            PacketLoss = reading.PacketLoss,
                            PacketLossCategory = reading.PacketLossCategory,
                            LatestSignalStrength = reading.LatestSignalStrength,
                            LatestSignalCategory = reading.LatestSignalCategory,
                            AverageSignalStrength = reading.AverageSignalStrength,
                            AverageSignalCategory = reading.AverageSignalCategory,
                            Firmware = reading.Firmware,
                            UpdatedAtUtc = reading.UpdatedAtUtc,
                            PacketLossStrength = reading.PacketLossStrength,
                            WifiIsRingNetwork = reading.WifiIsRingNetwork,
                            PtzConnected = reading.PtzConnected,
                            RingHealthId = reading.RingHealthId,
                            SupportedRpcCommands = (reading.SupportedRpcCommands ?? []).Select((c, i) => new DeviceHealthRpcCommand { Id = Guid.NewGuid(), Position = i, Command = c }).ToList()
                        };

                        bool wentOffline = await IsNewlyOfflineAsync(healthRepository, device.Id, reading.Connected, ct);
                        _ = await dataClient.RecordDeviceHealthAsync(health, ct);
                        // Point Devices.DeviceHealthId at the newest reading so the device row reflects its latest health.
                        await dataClient.RecordDeviceDetailsAsync(device.Id, health.Id, null, null, null, ct);
                        persisted++;
                        if (wentOffline)
                        {
                            if (!await NotifyDeviceOfflineAsync(sp, dispatcher, device, ct))
                            {
                                itemFailed = true;
                            }
                        }

                        // Readings more than 2 std devs from the camera's mean raise an alert. Base poll only: the elevated
                        // poll runs far more often and each check reads the camera's full history.
                        if (!await CheckSignalDeviationAsync(sp, device, ct))
                        {
                            itemFailed = true;
                        }

                        // Clip bit rates more than 2 std devs from the camera's mean raise an alert. Same base-poll-only rule as above.
                        if (!await CheckBitrateDeviationAsync(sp, device, ct))
                        {
                            itemFailed = true;
                        }

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
                                                "Health sync tick: device {DeviceName} RSSI degraded by {Degradation:F1} dB; entering elevated polling window",
                                                device.Name, degradation);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            itemFailed = true;
                            // Non-critical: don't let degradation check failure break the main tick
                            _logger.LogWarning("Health sync tick: degradation check for device {DeviceName} failed (non-critical): {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                        }
                    }

                    _logger.LogDebug(
                        "Health sync tick: {SourceType} returned {ReadingCount} reading(s), persisted {PersistedCount} metric(s)",
                        healthSource.GetType().Name, readings.Count, persisted);
                }
                catch (Exception ex)
                {
                    // One provider's failure must not stop another's - each health source gets its
                    // own try/catch, matching the plan's explicit call-out for this.
                    itemFailed = true;
                    _logger.LogWarning("Health sync tick: {SourceType} failed (non-critical): {Message} ({ErrorType})", healthSource.GetType().Name, ex.Message, ex.GetType().Name);
                }
            }
            if (itemFailed) { _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed"); } else { _heartbeats?.RecordRun(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow); }
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
            bool itemFailed = false;

            var healthSources = sp.GetServices<IProviderHealthSource>().ToList();
            if (healthSources.Count == 0)
            {
                return;
            }

            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            IVideoForensicsDataClient dataClient = sp.GetRequiredService<IVideoForensicsDataClient>();
            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            ISecurityAuditLogger auditLog = sp.GetRequiredService<ISecurityAuditLogger>();
            IDeviceHealthRepository? healthRepository = sp.GetService<IDeviceHealthRepository>();
            INotificationDispatcher? dispatcher = sp.GetService<INotificationDispatcher>();

            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devices;
            try
            {
                devices = await deviceRepository.ListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Elevated polling tick: failed to list devices; skipping this tick: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed");
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
                    _logger.LogDebug("Elevated polling tick: skipping {ProviderName} - provider API budget exceeded for this window", providerName);
                    continue;
                }

                try
                {
                    IReadOnlyList<DeviceHealthReading> readings = await healthSource.FetchHealthAsync(ct);
                    await budgetGuard.RecordCallAsync(providerName, ct);
                    int persisted = 0;

                    foreach (DeviceHealthReading reading in readings)
                    {
                        if (!devicesByProviderId.TryGetValue(reading.ProviderDeviceId, out Data.Common.Entities.Device? device) || !device.HealthCaptureEnabled)
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
                            BatteryVoltageValue = reading.BatteryVoltageValue,
                            IsExternalPowerConnected = reading.IsExternalPowerConnected,
                            OtaStatus = reading.OtaStatus,
                            LastHeartbeatUtc = reading.LastHeartbeatUtc,
                            CapturedAtUtc = DateTime.UtcNow,
                            DeviceType = reading.DeviceType,
                            FirmwareVersionStatus = reading.FirmwareVersionStatus,
                            BatteryPresent = reading.BatteryPresent,
                            BatteryPercentageCategory = reading.BatteryPercentageCategory,
                            BatteryVoltageCategory = reading.BatteryVoltageCategory,
                            RssiCategory = reading.RssiCategory,
                            NetworkConnectionValue = reading.NetworkConnectionValue,
                            PacketLoss = reading.PacketLoss,
                            PacketLossCategory = reading.PacketLossCategory,
                            LatestSignalStrength = reading.LatestSignalStrength,
                            LatestSignalCategory = reading.LatestSignalCategory,
                            AverageSignalStrength = reading.AverageSignalStrength,
                            AverageSignalCategory = reading.AverageSignalCategory,
                            Firmware = reading.Firmware,
                            UpdatedAtUtc = reading.UpdatedAtUtc,
                            PacketLossStrength = reading.PacketLossStrength,
                            WifiIsRingNetwork = reading.WifiIsRingNetwork,
                            PtzConnected = reading.PtzConnected,
                            RingHealthId = reading.RingHealthId,
                            SupportedRpcCommands = (reading.SupportedRpcCommands ?? []).Select((c, i) => new DeviceHealthRpcCommand { Id = Guid.NewGuid(), Position = i, Command = c }).ToList()
                        };

                        bool wentOffline = await IsNewlyOfflineAsync(healthRepository, device.Id, reading.Connected, ct);
                        _ = await dataClient.RecordDeviceHealthAsync(health, ct);
                        // Point Devices.DeviceHealthId at the newest reading so the device row reflects its latest health.
                        await dataClient.RecordDeviceDetailsAsync(device.Id, health.Id, null, null, null, ct);
                        persisted++;
                        if (wentOffline && !await NotifyDeviceOfflineAsync(sp, dispatcher, device, ct))
                        {
                            itemFailed = true;
                        }
                    }

                    if (persisted > 0)
                    {
                        _logger.LogDebug(
                            "Elevated polling tick: {SourceType} persisted {PersistedCount} metric(s) for elevated device(s)",
                            healthSource.GetType().Name, persisted);
                    }
                }
                catch (Exception ex)
                {
                    itemFailed = true;
                    _logger.LogWarning("Elevated polling tick: {SourceType} failed (non-critical): {Message} ({ErrorType})", healthSource.GetType().Name, ex.Message, ex.GetType().Name);
                }
            }

            // Now run jamming analysis and manage live view sessions for each elevated device.
            JammingToolsOrchestrator jammingOrchestrator = sp.GetRequiredService<JammingToolsOrchestrator>();
            ILiveViewSessionService liveViewService = sp.GetRequiredService<ILiveViewSessionService>();

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
                            _logger.LogDebug(
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
                    string deviceName = devices.FirstOrDefault(d => d.Id == deviceId)?.Name ?? "unknown device";
                    itemFailed = true;
                    _logger.LogWarning("Elevated polling tick: jamming analysis/live view management failed for device {DeviceName} (non-critical): {Message} ({ErrorType})", deviceName, ex.Message, ex.GetType().Name);
                }
            }

            if (itemFailed) { _heartbeats?.RecordError(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow, "run failed"); } else { _heartbeats?.RecordRun(nameof(DeviceHealthSyncService), DateTimeOffset.UtcNow); }
        }

        /// <summary>
        /// True when the reading is offline and the newest persisted reading was online. Checked before the new
        /// reading is written, so the alert fires on the transition rather than on every tick the device stays offline.
        /// </summary>
        private static async Task<bool> IsNewlyOfflineAsync(IDeviceHealthRepository? healthRepository, Guid deviceId, bool? connectedNow, CancellationToken ct)
        {
            // A null Connected means the provider did not say, so it is not treated as offline.
            if (connectedNow != false)
            {
                return false;
            }

            // Only a host that registers no health repository gets here without one; with no history there is no transition to report.
            if (healthRepository == null)
            {
                return false;
            }

            DeviceHealth? previous = await healthRepository.GetLatestAsync(deviceId, ct);
            return previous is { IsOnline: true };
        }

        /// <summary>
        /// Relative path of the sync page for the first provider account linked to the device's location, or null when none is linked.
        /// The page is where the device's polling mode is set. A relative path works in the app, where the user is signed in.
        /// </summary>
        private static async Task<string?> SyncPagePathAsync(IServiceProvider sp, VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            ILocationRepository? locations = sp.GetService<ILocationRepository>();
            if (locations is null)
            {
                return null;
            }

            IReadOnlyList<ProviderAccount> accounts = await locations.ListProviderAccountsForLocationAsync(device.LocationId, ct);
            return accounts.Count > 0 ? $"/accounts/{accounts[0].Id}/sync" : null;
        }

        /// <summary>Raises the DeviceOffline alert. A dispatch failure is logged and never undoes the health write that already happened.</summary>
        private async Task<bool> NotifyDeviceOfflineAsync(IServiceProvider sp, INotificationDispatcher? dispatcher, VideoForensics.Data.Common.Entities.Device device, CancellationToken ct)
        {
            if (dispatcher == null)
            {
                return true;
            }

            try
            {
                await dispatcher.DispatchAsync(new NotificationEvent(
                    EventType: "DeviceOffline",
                    TimestampUtc: DateTime.UtcNow,
                    OperatorId: null,
                    PairedDeviceId: device.Id,
                    SourceIp: null,
                    Details: $"{device.Name} went offline",
                    Audience: NotificationAudience.AdminsOnly,
                    Severity: NoticeSeverity.Alert,
                    ActionPath: await SyncPagePathAsync(sp, device, ct)), ct);
                _logger.LogInformation("Health sync: device {DeviceName} went offline; DeviceOffline alert dispatched", device.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Health sync: failed to dispatch offline alert for device {DeviceName} (non-critical): {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                return false;
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
