using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs <see cref="MediaFlagDeviationBackfill"/> once, a few minutes after startup, then stops. The pass is guarded by its
    /// own marker, so later starts find it complete and do nothing.
    /// </summary>
    public sealed class MediaFlagDeviationBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the pass reads the device history.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaFlagDeviationBackfillService> _logger;

        public MediaFlagDeviationBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaFlagDeviationBackfillService> logger)
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
                await RunPassAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown during the delay or the pass: the marker is not written, so the next start retries.
            }
        }

        /// <summary>Runs the pass in its own scope, since the repositories are scoped. A failure is logged; the next start retries.</summary>
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

                _ = await scope.ServiceProvider.GetRequiredService<MediaFlagDeviationBackfill>().RunOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Deviation backfill pass failed ({ExceptionType}): {Reason}", ex.GetType().Name, ex.Message);
            }
        }
    }
}
