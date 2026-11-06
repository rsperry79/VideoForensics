using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the BitrateCollapse flag backfill once per startup, a few minutes after startup. The pass is idempotent, so a
    /// restart that runs it again changes nothing. A failed pass is logged and not retried until the next startup.
    /// </summary>
    public class MediaFlagBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the pass reads recent clips.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaFlagBackfillService> _logger;

        public MediaFlagBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaFlagBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunPassAsync(stoppingToken);
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
