using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Services;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the analysis backfill on a timer, so media that was never analysed (old clips, and rows written without analysis,
    /// such as capture snapshots and roll-up loops) gets its jamming check. A failed pass is logged and the next pass retries.
    /// </summary>
    public class JammingAnalysisBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the first pass.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        /// <summary>Between passes. New unanalysed rows appear as captures and roll-ups are written, so 15 minutes keeps them current.</summary>
        private static readonly TimeSpan PassInterval = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JammingAnalysisBackfillService> _logger;

        /// <summary>Creates the service.</summary>
        public JammingAnalysisBackfillService(IServiceScopeFactory scopeFactory, ILogger<JammingAnalysisBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(PassInterval);
            do
            {
                await RunPassAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Runs one pass in its own scope, since the repository and analysis services are scoped.</summary>
        public async Task RunPassAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                _ = await scope.ServiceProvider.GetRequiredService<IJammingAnalysisBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis backfill pass failed; the next pass will retry");
            }
        }
    }
}
