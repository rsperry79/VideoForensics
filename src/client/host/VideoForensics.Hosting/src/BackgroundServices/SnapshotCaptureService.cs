using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the snapshot capture loop: every tick, it checks each active Ring account and hands it to <see cref="SnapshotCycle"/>,
    /// which decides which enabled cameras are due and captures them. The loop, the lease check and the per-tick logging live here;
    /// the capture, pause and hold logic lives in <see cref="SnapshotCycle"/>.
    ///
    /// Server-tier only (VideoForensics.Client.WebApp, registered through AddVideoForensicsSnapshotCapture). Logs use the provider name
    /// ("Ring account") and the device name, never account or device GUIDs.
    /// </summary>
    public class SnapshotCaptureService : BackgroundService
    {
        /// <summary>How often the loop checks which devices are due.</summary>
        internal static readonly TimeSpan TickPeriod = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SnapshotCaptureService> _logger;
        private readonly TimeProvider _time;
        private readonly SnapshotCycle _cycle;
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the capture service.</summary>
        public SnapshotCaptureService(
            IServiceScopeFactory scopeFactory,
            ILogger<SnapshotCaptureService> logger,
            TimeProvider time,
            SnapshotCycle cycle,
            ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
            _cycle = cycle;
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
                    _heartbeats?.Beat(nameof(SnapshotCaptureService), _time.GetUtcNow());
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
                        _heartbeats?.RecordError(nameof(SnapshotCaptureService), _time.GetUtcNow(), "run failed");
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

            bool itemFailed = false;
            IReadOnlyList<ProviderAccount> accounts = await sp.GetRequiredService<IProviderAccountRepository>().ListActiveAsync(ct);
            _logger.LogDebug("Snapshot capture tick started: {AccountCount} active accounts", accounts.Count);
            var counts = new SnapshotCycle.TickCounts();

            foreach (ProviderAccount account in accounts)
            {
                if (account.RemovedUtc != null || !account.IsActive
                    || !string.Equals(account.ProviderName, SnapshotCycle.RingProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ct.ThrowIfCancellationRequested();
                try
                {
                    await _cycle.CaptureAccountAsync(sp, account, counts, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One account's failure must never stop the others.
                    _cycle.HoldAccountAfterError(account.Id);
                    itemFailed = true;
                    _logger.LogError("Snapshot capture failed for {ProviderName} account: {Message} ({ErrorType})", account.ProviderName, ex.Message, ex.GetType().Name);
                }
            }

            if (counts.Attempted > 0)
            {
                _logger.LogDebug("Snapshot capture tick: {Captured} captured, {Failed} failed, {Throttled} throttled",
                    counts.Captured, counts.Failed, counts.Throttled);
            }
            if (counts.Failed > 0 || counts.AlertFailed)
            {
                itemFailed = true;
            }
            if (itemFailed) { _heartbeats?.RecordError(nameof(SnapshotCaptureService), _time.GetUtcNow(), "run failed"); } else { _heartbeats?.RecordRun(nameof(SnapshotCaptureService), _time.GetUtcNow()); }
        }
    }
}
