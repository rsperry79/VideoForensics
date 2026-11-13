using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// An operator's notification preferences as sent over <c>/api/v1/notifications/preferences</c>.
    /// </summary>
    /// <param name="PushEnabled">Whether Web Push notifications are enabled for the operator.</param>
    /// <param name="MinimumSeverity">Lowest <c>NoticeSeverity</c> value (0 Info to 3 Critical) that triggers a push.</param>
    /// <param name="EmailEnabled">Whether email notifications are enabled for the operator.</param>
    /// <param name="EmailEventTypes">Exact <c>SecurityAuditEventTypes</c> names that trigger an email.</param>
    public record NotificationPreferencesDto(
        bool PushEnabled,
        int MinimumSeverity,
        bool EmailEnabled,
        IReadOnlyList<string> EmailEventTypes
    );
}
