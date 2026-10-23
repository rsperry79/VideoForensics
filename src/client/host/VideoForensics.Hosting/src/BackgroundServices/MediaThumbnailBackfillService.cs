using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Services;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Makes sure every archived video has a stored thumbnail: once shortly after startup, then on a fixed interval,
    /// so videos imported before thumbnails existed, or whose frame extraction failed earlier, are picked up later.
    ///
    /// Registered in server-tier hosts only (console, MCP, VideoForensics.Client.WebApp) - never MAUI: the media files
    /// live on the server, and a client has no local path to write the thumbnail beside.
    /// </summary>
    public class MediaThumbnailBackfillService : BackgroundService
    {
        /// <summary>Lets startup finish before the first pass reads the whole archive.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        /// <summary>Between passes. Thumbnails only go missing after an import failure, so a long interval is enough.</summary>
        private static readonly TimeSpan PassInterval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaThumbnailBackfillService> _logger;

        /// <summary>Creates the service.</summary>
        public MediaThumbnailBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaThumbnailBackfillService> logger)
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

        /// <summary>
        /// Runs one backfill pass in its own scope, since the repository and services are scoped.
        /// A failed pass is logged and not rethrown, so the next scheduled pass still runs.
        /// </summary>
        public async Task RunPassAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IMediaThumbnailBackfill backfill = scope.ServiceProvider.GetRequiredService<IMediaThumbnailBackfill>();
                _ = await backfill.BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail backfill pass failed; the next pass will retry");
            }
        }
    }
}
