using Microsoft.AspNetCore.SignalR;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Periodically samples the Ring self-test status and pushes it to admin clients over
    /// <see cref="LiveHub"/>. Mirrors <see cref="DownloadProgressBroadcastService"/>: the timer only
    /// samples, and a payload is pushed only when it differs from the last one sent.
    /// <para>
    /// The status is resolved in a fresh scope on every tick, the same as the download broadcaster,
    /// so the service is not bound to a long-lived <see cref="IRingSelfTestService"/> instance.
    /// </para>
    /// </summary>
    public class SelfTestStatusBroadcastService : BackgroundService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly ISelfTestStatusChangeDetector _changeDetector;
        private readonly ILogger<SelfTestStatusBroadcastService> _logger;

        public SelfTestStatusBroadcastService(
            IServiceScopeFactory scopeFactory,
            IHubContext<LiveHub> hubContext,
            ISelfTestStatusChangeDetector changeDetector,
            ILogger<SelfTestStatusBroadcastService> logger)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _changeDetector = changeDetector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickInterval);
            SelfTestStatusDto? lastSent = null;
            do
            {
                try
                {
                    SelfTestStatusDto status;
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IRingSelfTestService selfTestService = scope.ServiceProvider.GetRequiredService<IRingSelfTestService>();
                        status = await selfTestService.GetStatusAsync(stoppingToken);
                    }

                    if (!_changeDetector.HasChanged(lastSent, status))
                    {
                        continue;
                    }

                    await _hubContext.Clients.Group("admins").SendAsync(LiveHubMethods.SelfTestStatus, status, stoppingToken);
                    lastSent = status;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Self-test status broadcast tick failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
