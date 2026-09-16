using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
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
    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IEnumerable<INotificationProvider> _providers;
        private readonly ILogger<NotificationDispatcher> _logger;
        private readonly INoticeRepository _noticeRepository;

        public NotificationDispatcher(IEnumerable<INotificationProvider> providers, ILogger<NotificationDispatcher> logger, INoticeRepository noticeRepository)
        {
            _providers = providers;
            _logger = logger;
            _noticeRepository = noticeRepository;
        }

        public async Task DispatchAsync(NotificationEvent notificationEvent, CancellationToken ct)
        {
            // Persist notice to database before dispatching to providers
            try
            {
                var notice = new Notice
                {
                    Id = Guid.NewGuid(),
                    EventType = notificationEvent.EventType,
                    Severity = notificationEvent.Severity,
                    Details = notificationEvent.Details,
                    Audience = (int)notificationEvent.Audience,  // cast enum to int per Notice schema
                    TimestampUtc = notificationEvent.TimestampUtc,
                    OperatorId = notificationEvent.OperatorId,
                    ProviderAccountId = null  // will be added in a future task
                };
                await _noticeRepository.AddAsync(notice, ct);
                _logger.LogInformation("Persisted notification event {EventType} to notice repository", notificationEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist notice event {EventType} to database (non-critical, continuing with provider dispatch)", notificationEvent.EventType);
            }

            foreach (INotificationProvider provider in _providers)
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
