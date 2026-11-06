using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Core.Contracts;

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
        private readonly ThreadHeartbeats? _heartbeats;
        private readonly IForensicsConfiguration? _config;

        /// <summary>The wait between passes: the Services page setting when present, otherwise the built-in interval.</summary>
        private TimeSpan CurrentInterval => _config is null ? PassInterval : ScheduledReportService.IntervalFor(_config.MediaBackfillIntervalHours);

        public MediaThumbnailBackfillService(IServiceScopeFactory scopeFactory, ILogger<MediaThumbnailBackfillService> logger, ThreadHeartbeats? heartbeats = null, IForensicsConfiguration? config = null)
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
                _heartbeats?.Beat(nameof(MediaThumbnailBackfillService), DateTimeOffset.UtcNow, CurrentInterval);
                await RunPassAsync(stoppingToken);
            }
            while (await WaitForNextPassAsync(stoppingToken));
        }

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

                IMediaThumbnailBackfill backfill = scope.ServiceProvider.GetRequiredService<IMediaThumbnailBackfill>();
                _ = await backfill.BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Thumbnail backfill pass failed; the next pass will retry: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
            }
        }
    }
}
