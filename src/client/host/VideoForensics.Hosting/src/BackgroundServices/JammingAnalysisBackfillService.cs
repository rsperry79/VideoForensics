using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Core.Contracts;
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

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JammingAnalysisBackfillService> _logger;
        private readonly ThreadHeartbeats? _heartbeats;
        private readonly IForensicsConfiguration _config;

        /// <summary>The wait between passes, read from the Services page setting before each wait.</summary>
        internal TimeSpan CurrentInterval => TimeSpan.FromMinutes(_config.JammingIntervalMinutes);

        /// <summary>Creates the service.</summary>
        public JammingAnalysisBackfillService(IServiceScopeFactory scopeFactory, ILogger<JammingAnalysisBackfillService> logger, IForensicsConfiguration config, ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _heartbeats = heartbeats;
            _config = config;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            do
            {
                _heartbeats?.Beat(nameof(JammingAnalysisBackfillService), DateTimeOffset.UtcNow, CurrentInterval);
                await RunPassAsync(stoppingToken);
            }
            while (await WaitForNextPassAsync(stoppingToken));
        }

        /// <summary>
        /// Waits the configured interval, read now so a changed setting applies from the next pass. A fresh delay each
        /// pass is used instead of a PeriodicTimer, which is fixed at creation and would ignore the change.
        /// </summary>
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

        /// <summary>Runs one pass in its own scope, since the repository and analysis services are scoped.</summary>
        public async Task RunPassAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                using IDisposable? lease = scope.ServiceProvider.TryEnter();
                if (lease is null)
                {
                    return;
                }

                _ = await scope.ServiceProvider.GetRequiredService<IJammingAnalysisBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Analysis backfill pass failed; the next pass will retry: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
            }
        }
    }
}
