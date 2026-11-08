using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the snapshot sync loop: every tick, it asks <see cref="ISnapshotSyncService.RunCycleAsync"/> for one capture pass over the
    /// active Ring accounts. The loop, the lease check, the heartbeats and the per-tick logging live here; the capture, pause and hold
    /// logic lives in <see cref="SnapshotCycle"/>, which the sync service runs.
    ///
    /// Server-tier only (VideoForensics.Client.WebApp, registered through AddVideoForensicsSnapshotCapture). Logs use the provider name
    /// ("Ring account") and the device name, never account or device GUIDs.
    /// </summary>
    public class SnapshotSyncBackgroundService : BackgroundService
    {
        /// <summary>How often the loop checks which devices are due.</summary>
        internal static readonly TimeSpan TickPeriod = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SnapshotSyncBackgroundService> _logger;
        private readonly TimeProvider _time;
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the background service.</summary>
        public SnapshotSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<SnapshotSyncBackgroundService> logger,
            TimeProvider time,
            ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
            _heartbeats = heartbeats;
        }

        /// <summary>
        /// True for the Ring device types that carry a camera. Allowlisted rather than denylisted: a chime, a range extender or any
        /// unrecognised type has no camera to snapshot, so it is skipped by default.
        /// </summary>
        internal static bool HasCamera(string? type)
        {
            return DeviceKinds.IsCamera(type);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickPeriod, _time);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _heartbeats?.Beat(nameof(SnapshotSyncBackgroundService), _time.GetUtcNow());
                    try
                    {
                        await RunOneTickAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _heartbeats?.RecordError(nameof(SnapshotSyncBackgroundService), _time.GetUtcNow(), "run failed");
                        _logger.LogError("Snapshot capture tick failed; will retry on the next tick: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>Internal for direct testability (VideoForensics.Client.Hosting.Tests) without driving the BackgroundService timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;
            using IDisposable? lease = sp.TryEnter();
            if (lease is null)
            {
                // Another sync or maintenance holds the lease, so this tick captures nothing. Logged so a silent gap is visible.
                _logger.LogInformation("Snapshot capture tick skipped: another operation holds the lease");
                return;
            }

            SnapshotCycle.TickCounts counts = await sp.GetRequiredService<ISnapshotSyncService>().RunCycleAsync(ct);
            if (counts.Attempted > 0)
            {
                _logger.LogDebug("Snapshot capture tick: {Captured} captured, {Failed} failed, {Throttled} throttled",
                    counts.Captured, counts.Failed, counts.Throttled);
            }

            bool itemFailed = counts.Failed > 0 || counts.AlertFailed || counts.AccountFailed;
            if (itemFailed) { _heartbeats?.RecordError(nameof(SnapshotSyncBackgroundService), _time.GetUtcNow(), "run failed"); } else { _heartbeats?.RecordRun(nameof(SnapshotSyncBackgroundService), _time.GetUtcNow()); }
        }
    }
}
