namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>A security-audit event (plan §5.5) being fanned out to notification channels (plan §5.6).</summary>
    public record NotificationEvent(
        string EventType,
        DateTime TimestampUtc,
        Guid? OperatorId,
        Guid? PairedDeviceId,
        string? SourceIp,
        string? Details);

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
}
