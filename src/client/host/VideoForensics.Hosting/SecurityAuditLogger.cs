using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>Thin convenience wrapper over ISecurityAuditLogRepository so call sites don't hand-build a SecurityAuditLogEntry every time (plan §5.5).</summary>
    public interface ISecurityAuditLogger
    {
        Task LogAsync(string eventType, Guid? operatorId, Guid? pairedDeviceId, string? sourceIp, string? details, bool isUrgent, CancellationToken ct);
    }

    /// <summary>
    /// Persists the entry, then fans it out to <see cref="INotificationDispatcher"/> if urgent
    /// (plan §5.6) - the one choke point every security event already passes through, so a new
    /// urgent event type is automatically notification-worthy without a second call site to
    /// remember. The urgency a caller passes is only the DEFAULT for that event type: a per-event
    /// override set via <see cref="IUrgencyOverrideStore"/> (the Notifications settings screen)
    /// takes precedence, satisfying the plan's "configurable per event type" requirement without
    /// hardcoding urgency into each call site.
    /// </summary>
    public class SecurityAuditLogger : ISecurityAuditLogger
    {
        private readonly ISecurityAuditLogRepository _repository;
        private readonly IUrgencyOverrideStore _urgencyOverrides;
        private readonly INotificationDispatcher _notificationDispatcher;
        private readonly ILogger<SecurityAuditLogger> _logger;

        public SecurityAuditLogger(
            ISecurityAuditLogRepository repository,
            IUrgencyOverrideStore urgencyOverrides,
            INotificationDispatcher notificationDispatcher,
            ILogger<SecurityAuditLogger> logger)
        {
            _repository = repository;
            _urgencyOverrides = urgencyOverrides;
            _notificationDispatcher = notificationDispatcher;
            _logger = logger;
        }

        public async Task LogAsync(string eventType, Guid? operatorId, Guid? pairedDeviceId, string? sourceIp, string? details, bool isUrgent, CancellationToken ct)
        {
            var effectiveUrgent = await _urgencyOverrides.GetOverrideAsync(eventType, ct) ?? isUrgent;

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = eventType,
                OperatorId = operatorId,
                PairedDeviceId = pairedDeviceId,
                SourceIp = sourceIp,
                Details = details,
                IsUrgent = effectiveUrgent
            }, ct);

            if (!effectiveUrgent)
            {
                return;
            }

            try
            {
                await _notificationDispatcher.DispatchAsync(
                    new NotificationEvent(eventType, DateTime.UtcNow, operatorId, pairedDeviceId, sourceIp, details), ct);
            }
            catch (Exception ex)
            {
                // Notification delivery failing must never fail (or roll back) the audit-log write
                // that triggered it - the event is already durably recorded above.
                _logger.LogError(ex, "Failed to dispatch notifications for security event {EventType}", eventType);
            }
        }
    }
}
