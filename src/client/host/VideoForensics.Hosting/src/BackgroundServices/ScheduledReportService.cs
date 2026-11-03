using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Writes the enabled forensic reports on a schedule (Services page, "Run report every x hours"). Each run covers the
    /// previous 24 hours, whatever the interval. The interval is read on every cycle, so a change applies without a restart.
    /// Report types are gated by their own toggles on the Reports page, and the run is gated by the Services toggle.
    /// Not per provider: this service reads evidence the server already holds, so it does not call any provider API.
    /// </summary>
    public sealed class ScheduledReportService : BackgroundService
    {
        internal static readonly TimeSpan ReportWindow = TimeSpan.FromHours(24);
        internal const int MinIntervalHours = 1;
        internal const int MaxIntervalHours = 168;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IForensicsConfiguration _config;
        private readonly ILogger<ScheduledReportService> _logger;
        private readonly TimeProvider _time;

        public ScheduledReportService(IServiceScopeFactory scopeFactory, IForensicsConfiguration config, ILogger<ScheduledReportService> logger, TimeProvider time)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
            _time = time;
        }

        /// <summary>The 24-hour window a run covers, ending at <paramref name="utcNow"/>.</summary>
        internal static (DateTime FromUtc, DateTime ToUtc) WindowEndingAt(DateTime utcNow)
        {
            return (utcNow - ReportWindow, utcNow);
        }

        /// <summary>The wait between runs for a configured hour count, clamped to the allowed range.</summary>
        internal static TimeSpan IntervalFor(int configuredHours)
        {
            return TimeSpan.FromHours(Math.Clamp(configuredHours, MinIntervalHours, MaxIntervalHours));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(IntervalFor(_config.ScheduledReportIntervalHours), _time, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                if (!_config.EnableScheduledReports)
                {
                    continue;
                }

                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled report run failed; it will run again at the next interval");
                }
            }
        }

        /// <summary>Writes each enabled report for the last 24 hours. Internal so tests can run it without waiting.</summary>
        internal async Task RunOnceAsync(CancellationToken ct)
        {
            (DateTime fromUtc, DateTime toUtc) = WindowEndingAt(_time.GetUtcNow().UtcDateTime);
            using IServiceScope scope = _scopeFactory.CreateScope();
            IReportGenerationService reports = scope.ServiceProvider.GetRequiredService<IReportGenerationService>();
            string format = _config.ReportOutputFormat;

            if (_config.EnableForensicAnalysisReports)
            {
                await reports.WriteReportAsync(await reports.BuildForensicAnalysisReportAsync(null, fromUtc, toUtc, ct), format, ct);
            }

            if (_config.EnableSignalAnomalyReports)
            {
                await reports.WriteReportAsync(await reports.BuildSignalAnomalyReportAsync(null, fromUtc, toUtc, ct), format, ct);
            }

            if (_config.EnableChainOfCustodyReports)
            {
                await reports.WriteReportAsync(await reports.BuildChainOfCustodyReportAsync(null, fromUtc, toUtc, ct), format, ct);
            }

            if (_config.EnableEvidenceValidationReports)
            {
                await reports.WriteReportAsync(await reports.BuildEvidenceReviewAsync(null, fromUtc, toUtc, ct), format, ct);
            }

            if (_config.EnableAccessControlMonitoring)
            {
                await reports.WriteReportAsync(await reports.BuildAccessControlReportAsync(null, fromUtc, toUtc, ct), format, ct);
            }

            _logger.LogInformation("Scheduled reports written for {From} to {To}", fromUtc, toUtc);
        }
    }
}
