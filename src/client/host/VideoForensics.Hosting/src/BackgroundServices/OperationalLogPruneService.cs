using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Deletes old operational rows once a day: provider API error rows, provider call-log rows, and live-view telemetry.
    /// These grow without limit and carry no audit value. Audit and action-log tables are not touched, because they are
    /// hash-chained and pruning them would break the chain.
    /// </summary>
    public sealed class OperationalLogPruneService : BackgroundService
    {
        internal static readonly TimeSpan Interval = TimeSpan.FromDays(1);
        internal static readonly TimeSpan ErrorRetention = TimeSpan.FromDays(30);
        internal static readonly TimeSpan CallLogRetention = TimeSpan.FromDays(2);
        internal static readonly TimeSpan TelemetryRetention = TimeSpan.FromDays(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OperationalLogPruneService> _logger;
        private readonly TimeProvider _time;
        private readonly ThreadHeartbeats? _heartbeats;
        private readonly IForensicsConfiguration? _config;

        /// <summary>The wait between passes: the Services page setting when present, otherwise the built-in interval.</summary>
        private TimeSpan CurrentInterval => _config is null ? Interval : ScheduledReportService.IntervalFor(_config.LogPruneIntervalHours);

        public OperationalLogPruneService(IServiceScopeFactory scopeFactory, ILogger<OperationalLogPruneService> logger, TimeProvider time, ThreadHeartbeats? heartbeats = null, IForensicsConfiguration? config = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
            _heartbeats = heartbeats;
            _config = config;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                _heartbeats?.Beat(nameof(OperationalLogPruneService), _time.GetUtcNow(), CurrentInterval);
                try
                {
                    await PruneOnceAsync(stoppingToken);
                    _heartbeats?.RecordRun(nameof(OperationalLogPruneService), _time.GetUtcNow());
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _heartbeats?.RecordError(nameof(OperationalLogPruneService), _time.GetUtcNow(), "run failed");
                    _logger.LogError("Operational log prune failed; it will run again at the next interval: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                }
            }
            while (await WaitForNextPassAsync(stoppingToken));
        }

        private async Task<bool> WaitForNextPassAsync(CancellationToken ct)
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

        /// <summary>Runs one prune pass. Internal so tests can call it without waiting a day.</summary>
        internal async Task PruneOnceAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            int errorRows = await sp.GetRequiredService<IProviderApiErrorLogRepository>().PruneOlderThanAsync(ErrorRetention, ct);
            await sp.GetRequiredService<IProviderApiCallLogRepository>().PruneOlderThanAsync(CallLogRetention, ct);
            int telemetrySamples = await sp.GetRequiredService<ILiveViewTelemetryRepository>().PruneOlderThanAsync(TelemetryRetention, ct);

            _logger.LogInformation("Pruned operational logs: {ErrorRows} error rows, {TelemetrySamples} telemetry samples", errorRows, telemetrySamples);
        }
    }
}
