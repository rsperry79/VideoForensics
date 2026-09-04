using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Fans an urgent security event out to every enabled <see cref="INotificationProvider"/>
    /// (plan §5.6). Called from <see cref="SecurityAuditLogger"/> - the one place every security
    /// event already passes through - rather than from each individual call site, so adding a new
    /// urgent event type never requires remembering to also wire up notification dispatch for it.
    /// One provider's failure is isolated and logged, not allowed to block the others or bubble up
    /// into the security-audit-logging call site that triggered it.
    /// </summary>
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationEvent notificationEvent, CancellationToken ct);
    }

    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IEnumerable<INotificationProvider> _providers;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(IEnumerable<INotificationProvider> providers, ILogger<NotificationDispatcher> logger)
        {
            _providers = providers;
            _logger = logger;
        }

        public async Task DispatchAsync(NotificationEvent notificationEvent, CancellationToken ct)
        {
            foreach (var provider in _providers)
            {
                try
                {
                    if (await provider.IsEnabledAsync(ct))
                    {
                        await provider.SendAsync(notificationEvent, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Notification provider {Provider} failed to send event {EventType}", provider.Name, notificationEvent.EventType);
                }
            }
        }
    }
}
