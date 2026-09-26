using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Hosting.BackgroundServices
{
    /// <summary>
    /// Periodically calibrates camera bitrate baselines by launching short live-view sessions
    /// for devices whose time-bucketed bitrate data is undersample or stale. Calibration helps
    /// the interference detection system establish accurate baselines for each device, time-of-day,
    /// and weekday/weekend patterns.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.WebApp) - never MAUI:
    /// calibration calls a provider's API directly via live-view, and per §1's "only the server
    /// pulls from any provider" rule, no client host may do that.
    /// </summary>
    public class CameraBitrateCalibrationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly ILogger<CameraBitrateCalibrationService> _logger;

        public CameraBitrateCalibrationService(
            IServiceScopeFactory scopeFactory,
            IForensicsConfiguration config,
            ILogger<CameraBitrateCalibrationService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_config.CalibrationCheckIntervalMinutes));

            do
            {
                await RunOneTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Internal for direct testability (VideoForensics.Hosting.Tests) without driving the whole BackgroundService lifecycle/timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            if (!_config.EnableBitrateCalibration)
            {
                _logger.LogDebug("Bitrate calibration is disabled via configuration; skipping this tick");
                return;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            ICameraBitrateBaselineRepository baselineRepo = sp.GetRequiredService<ICameraBitrateBaselineRepository>();
            ILiveViewSessionService sessionService = sp.GetRequiredService<ILiveViewSessionService>();
            IProviderApiBudgetGuard budgetGuard = sp.GetRequiredService<IProviderApiBudgetGuard>();
            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();

            // Fetch buckets that need calibration (undersampled or stale)
            IReadOnlyList<(Guid DeviceId, int HourOfDay, bool IsWeekend)> bucketsNeedingCalibration;
            try
            {
                bucketsNeedingCalibration = await baselineRepo.ListBucketsNeedingCalibrationAsync(
                    _config.MinCalibrationSamplesPerBucket,
                    TimeSpan.FromDays(_config.CalibrationStalenessDays),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bitrate calibration tick: failed to list buckets needing calibration; skipping this tick");
                return;
            }

            if (bucketsNeedingCalibration.Count == 0)
            {
                return;
            }

            // Load all devices to map DeviceId -> Device (for provider name lookup)
            IReadOnlyList<Device> devices;
            try
            {
                devices = await deviceRepository.ListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bitrate calibration tick: failed to list devices; skipping this tick");
                return;
            }

            if (devices.Count == 0)
            {
                return;
            }

            var devicesById = devices.ToDictionary(d => d.Id, d => d);

            // Filter buckets to only those for the current UTC hour and current weekday/weekend status
            int currentHour = DateTime.UtcNow.Hour;
            bool isCurrentlyWeekend = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var currentHourBuckets = bucketsNeedingCalibration
                .Where(b => b.HourOfDay == currentHour && b.IsWeekend == isCurrentlyWeekend)
                .ToList();

            if (currentHourBuckets.Count == 0)
            {
                return;
            }

            // Process each bucket independently (per-device isolation)
            foreach (var (deviceId, _, _) in currentHourBuckets)
            {
                if (!devicesById.TryGetValue(deviceId, out Device? device))
                {
                    _logger.LogWarning("Bitrate calibration tick: bucket references unknown device {DeviceId}; skipping", deviceId);
                    continue;
                }

                try
                {
                    // Skip if device already has an active session (any reason)
                    LiveViewSession? activeSession = await sessionService.GetActiveSessionAsync(deviceId, ct);
                    if (activeSession != null)
                    {
                        _logger.LogDebug("Bitrate calibration tick: device {DeviceName} has active session; skipping calibration", device.Name);
                        continue;
                    }

                    // TODO: Device has no ProviderName field yet; hardcode "Ring" for now.
                    // In production, provider name comes from Device.ProviderName or via Location lookup.
                    string providerName = "Ring";

                    // Check budget guard before calling provider
                    if (!await budgetGuard.TryConsumeAsync(providerName, ct))
                    {
                        _logger.LogWarning("Bitrate calibration tick: skipping device {DeviceName} - provider API budget exceeded for {ProviderName}", device.Name, providerName);
                        continue;
                    }

                    // Start a calibration session
                    _ = await sessionService.StartAsync(deviceId, LiveViewTriggerReason.Calibration, null, ct);
                    _logger.LogInformation("Bitrate calibration tick: started calibration session for device {DeviceName}", device.Name);
                }
                catch (Exception ex)
                {
                    // One device's failure must not stop others - each device gets its own try/catch
                    _logger.LogWarning(ex, "Bitrate calibration tick: device {DeviceName} failed (non-critical)", device.Name);
                }
            }
        }
    }
}
