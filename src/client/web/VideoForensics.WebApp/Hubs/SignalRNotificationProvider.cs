using Microsoft.AspNetCore.SignalR;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// Pushes urgent security events (plan §5.6) to every connected <see cref="LiveHub"/> client -
    /// on MAUI, this is what drives an OS toast notification while the app is running. Reuses the
    /// same <see cref="INotificationProvider"/> extensibility point as email rather than adding a
    /// second dispatch path: <see cref="NotificationDispatcher"/> already fans out to every
    /// registered provider, so this is a new provider class, not new dispatch machinery.
    /// </summary>
    public class SignalRNotificationProvider : INotificationProvider
    {
        private readonly IHubContext<LiveHub> _hubContext;

        public SignalRNotificationProvider(IHubContext<LiveHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public string Name => "SignalR";

        // Always enabled - sending to zero connected clients is a harmless no-op, and unlike email
        // there is no configuration step (SMTP host/credentials) that could be missing.
        public Task<bool> IsEnabledAsync(CancellationToken ct) => Task.FromResult(true);

        public Task SendAsync(NotificationEvent notificationEvent, CancellationToken ct)
            => _hubContext.Clients.All.SendAsync("UrgentEvent", notificationEvent, ct);
    }
}
