using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Core.Contracts;

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

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaCodecBackfillService> _logger;
        private readonly ThreadHeartbeats? _heartbeats;
        private readonly IForensicsConfiguration _config;

        /// <summary>The wait between passes, read from the Services page setting before each wait. Each pass probes one batch, so the backlog clears over several passes.</summary>
        internal TimeSpan CurrentInterval => TimeSpan.FromMinutes(_config.CodecIntervalMinutes);

        /// <summary>Creates the service.</summary>
        public MediaCodecBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaCodecBackfillService> logger, IForensicsConfiguration config, ThreadHeartbeats? heartbeats = null)
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
                _heartbeats?.Beat(nameof(MediaCodecBackfillService), DateTimeOffset.UtcNow, CurrentInterval);
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

                _ = await scope.ServiceProvider.GetRequiredService<IMediaCodecBackfill>().BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Codec backfill pass failed; the next pass will retry: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
            }
        }
    }
}
