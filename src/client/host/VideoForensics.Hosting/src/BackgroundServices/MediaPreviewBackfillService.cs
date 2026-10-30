using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Makes sure every archived video has a preview MP4: once shortly after startup, then on a fixed interval, so clips
    /// downloaded before previews existed, or whose preview failed earlier, are picked up later.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.Client.WebApp) - never MAUI: the media files
    /// live on the server, and ffmpeg writes the preview beside the clip.
    /// </summary>
    public class MediaPreviewBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the first pass reads the archive.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        /// <summary>Between passes. Previews only go missing after an earlier encode failure, so a long interval is enough.</summary>
        private static readonly TimeSpan PassInterval = TimeSpan.FromHours(6);

        /// <summary>Clips requested from the repository per batch. Each clip is one ffmpeg run, so batches stay small.</summary>
        private const int BatchSize = 25;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaPreviewBackfillService> _logger;
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the service.</summary>
        public MediaPreviewBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaPreviewBackfillService> logger, ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _heartbeats = heartbeats;
            _logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(PassInterval);
            do
            {
                _heartbeats?.Beat(nameof(MediaPreviewBackfillService), DateTimeOffset.UtcNow, PassInterval);
                await RunPassAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>
        /// Runs one backfill pass in its own scope, since the repository and services are scoped.
        /// A failed pass is logged and not rethrown, so the next scheduled pass still runs.
        /// </summary>
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

                IMediaPreviewBackfill backfill = scope.ServiceProvider.GetRequiredService<IMediaPreviewBackfill>();
                MediaPreviewBackfillResult result = await backfill.RunAsync(BatchSize, ct);
                _logger.LogInformation(
                    "Preview backfill: processed {Processed} video(s), {Generated} generated, {Failed} failed",
                    result.Processed, result.Generated, result.Failed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preview backfill pass failed; the next pass will retry");
            }
        }
    }
}
