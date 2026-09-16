namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Audience routing for notification events.</summary>
    public enum NotificationAudience
    {
        /// <summary>Send to all connected clients (default).</summary>
        All = 0,

        /// <summary>Send only to admin-and-above users.</summary>
        AdminsOnly = 1
    }

    /// <summary>A security-audit event (plan §5.5) being fanned out to notification channels (plan §5.6).</summary>
    public record NotificationEvent(
        string EventType,
        DateTime TimestampUtc,
        Guid? OperatorId,
        Guid? PairedDeviceId,
        string? SourceIp,
        string? Details,
        NotificationAudience Audience = NotificationAudience.All,
        NoticeSeverity Severity = NoticeSeverity.Info);

    /// <summary>
    /// A pluggable urgent-notification channel (plan §5.6) - email, Web Push, MAUI toast, or an
    /// opt-in third-party relay (ntfy.sh, Pushover, etc.) all implement this same small contract,
    /// so adding a new channel later is a new class, not a redesign of the dispatch pipeline.
    /// </summary>
    public interface INotificationProvider
    {
        /// <summary>Short, stable identifier for this channel (e.g. "Email"), used in settings/logs.</summary>
        string Name { get; }

        /// <summary>Whether this channel is currently configured and enabled - checked before every dispatch.</summary>
        Task<bool> IsEnabledAsync(CancellationToken ct);

        /// <summary>Delivers the event. Implementations should let exceptions propagate - the dispatcher isolates one channel's failure from the others.</summary>
        Task SendAsync(NotificationEvent notificationEvent, CancellationToken ct);
    }

    /// <summary>
    /// Fans a notification event out to every enabled INotificationProvider
    /// (plan §5.6). One provider's failure is isolated and logged, not allowed to block the others or bubble up.
    /// </summary>
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationEvent notificationEvent, CancellationToken ct);
    }
}
