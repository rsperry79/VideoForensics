using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Runs the codec backfill on a timer, so media written before codec, resolution and frame rate were stored gets them.
    /// A failed pass is logged and the next pass retries.
    /// </summary>
    public class MediaCodecBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the first pass.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        /// <summary>Between passes. Each pass probes one batch, so the backlog clears over several passes.</summary>
        private static readonly TimeSpan PassInterval = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaCodecBackfillService> _logger;
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the service.</summary>
        public MediaCodecBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaCodecBackfillService> logger, ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _heartbeats = heartbeats;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(PassInterval);
            do
            {
                _heartbeats?.Beat(nameof(MediaCodecBackfillService), DateTimeOffset.UtcNow, PassInterval);
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
                using IDisposable? lease = scope.ServiceProvider.TryEnter();
                if (lease is null)
                {
                    return;
                }

                _ = await scope.ServiceProvider.GetRequiredService<IMediaCodecBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Codec backfill pass failed; the next pass will retry");
            }
        }
    }
}
