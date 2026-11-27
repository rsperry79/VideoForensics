using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the placeholder snapshot backfill once, a few minutes after startup, then stops. The pass is idempotent, so a later
    /// start marks only rows the earlier run could not reach.
    /// </summary>
    public class PlaceholderSnapshotBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the pass takes the write gate.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlaceholderSnapshotBackfillService> _logger;

        /// <summary>Creates the service.</summary>
        public PlaceholderSnapshotBackfillService(IServiceScopeFactory scopeFactory, ILogger<PlaceholderSnapshotBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }

        /// <summary>Runs the pass once in its own scope, since the repository is scoped.</summary>
        public async Task RunOnceAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                using IDisposable? lease = scope.ServiceProvider.TryEnter();
                if (lease is null)
                {
                    return;
                }

                _ = await scope.ServiceProvider.GetRequiredService<IPlaceholderSnapshotBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Placeholder snapshot backfill pass failed ({ExceptionType}): {Reason}", ex.GetType().Name, ex.Message);
            }
        }
    }
}
