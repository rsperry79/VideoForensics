using Microsoft.AspNetCore.SignalR;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Periodically pushes the server's own download progress out over <see cref="LiveHub"/> (plan
    /// §6) - a remote paired client (MAUI) has no other way to see live progress, since it isn't
    /// the process actually running the download (only the server executes downloads, plan §1).
    /// A tick with nothing to report is cheap (one scoped <see cref="IVideoDownloadService"/>
    /// resolution + four already-in-memory getters), so a fixed short interval is used rather than
    /// only ticking while a download happens to be running.
    /// <para>
    /// Send-on-change: the timer only samples. A payload is pushed when its state differs from the
    /// last one sent, or when it carries activity lines (which are drained here, the single consumer,
    /// and must reach every client).
    /// </para>
    /// </summary>
    public class DownloadProgressBroadcastService : BackgroundService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(750);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly IDownloadProgressChangeDetector _changeDetector;
        private readonly ILogger<DownloadProgressBroadcastService> _logger;

        public DownloadProgressBroadcastService(
            IServiceScopeFactory scopeFactory,
            IHubContext<LiveHub> hubContext,
            IDownloadProgressChangeDetector changeDetector,
            ILogger<DownloadProgressBroadcastService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _changeDetector = changeDetector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickInterval);
            DownloadProgressDto? lastSent = null;
            do
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    IVideoDownloadService downloadService = scope.ServiceProvider.GetRequiredService<IVideoDownloadService>();

                    DownloadProgressDto payload = downloadService.ToDownloadProgressDto();

                    if (!_changeDetector.ShouldSend(lastSent, payload))
                    {
                        continue;
                    }

                    await _hubContext.Clients.All.SendAsync("DownloadProgress", payload, stoppingToken);
                    lastSent = payload;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Download progress broadcast tick failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
