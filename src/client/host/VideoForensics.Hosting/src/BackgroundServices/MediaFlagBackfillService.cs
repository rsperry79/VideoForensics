using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the BitrateCollapse flag backfill on a timer, a few minutes after startup and then between passes. The pass is
    /// idempotent, so a repeat that finds nothing new changes nothing. A failed pass is logged and the next pass retries.
    /// </summary>
    public class MediaFlagBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the pass reads recent clips.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaFlagBackfillService> _logger;
        private readonly ThreadHeartbeats? _heartbeats;
        private readonly IForensicsConfiguration _config;

        /// <summary>The wait between passes, read from the Services page setting before each wait.</summary>
        internal TimeSpan CurrentInterval => TimeSpan.FromMinutes(_config.FlagIntervalMinutes);

        /// <summary>Creates the service.</summary>
        public MediaFlagBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaFlagBackfillService> logger, IForensicsConfiguration config, ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _heartbeats = heartbeats;
            _config = config;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            do
            {
                _heartbeats?.Beat(nameof(MediaFlagBackfillService), DateTimeOffset.UtcNow, CurrentInterval);
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

        /// <summary>Runs one pass in its own scope, since the repository is scoped.</summary>
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

                _ = await scope.ServiceProvider.GetRequiredService<IMediaFlagBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Flag backfill pass failed ({ExceptionType}): {Reason}", ex.GetType().Name, ex.Message);
            }
        }
    }
}
