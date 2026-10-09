using Microsoft.AspNetCore.SignalR;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Periodically pushes the server's own download progress out over <see cref="LiveHub"/> (plan
    /// §6) - a remote paired client (MAUI) has no other way to see live progress, since it isn't
    /// the process actually running the download (only the server executes downloads, plan §1).
    /// A tick with nothing to report is cheap (one scoped <see cref="IVideoDownloadService"/>
    /// resolution + four already-in-memory getters), so a fixed short interval is used rather than
    /// only ticking while a download happens to be running.
    /// </summary>
    public class DownloadProgressBroadcastService : BackgroundService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(750);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly ILogger<DownloadProgressBroadcastService> _logger;

        public DownloadProgressBroadcastService(
            IServiceScopeFactory scopeFactory,
            IHubContext<LiveHub> hubContext,
            ILogger<DownloadProgressBroadcastService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    IVideoDownloadService downloadService = scope.ServiceProvider.GetRequiredService<IVideoDownloadService>();

                    DownloadProgressDto payload = downloadService.ToDownloadProgressDto();

                    await _hubContext.Clients.All.SendAsync("DownloadProgress", payload, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Download progress broadcast tick failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
