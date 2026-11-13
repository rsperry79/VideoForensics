using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping notification preference entities to and from DTOs.</summary>
    public static class NotificationPreferencesDtoMapping
    {
        /// <summary>
        /// Converts a stored preference row to a DTO. Event type names are returned sorted so the response is stable.
        /// </summary>
        public static NotificationPreferencesDto ToDto(this OperatorNotificationPreference entity)
        {
            return new NotificationPreferencesDto(
                PushEnabled: entity.PushEnabled,
                MinimumSeverity: (int)entity.MinimumSeverity,
                EmailEnabled: entity.EmailEnabled,
                EmailEventTypes: entity.EmailEventTypeSet.OrderBy(name => name, StringComparer.Ordinal).ToList()
            );
        }

        /// <summary>
        /// Converts a DTO to a preference entity. <c>OperatorId</c> is left unset; the caller sets it from the authenticated operator.
        /// The DTO must already be validated (see the endpoint), because an out-of-range severity is cast without checking.
        /// </summary>
        public static OperatorNotificationPreference ToDomain(this NotificationPreferencesDto dto)
        {
            return new OperatorNotificationPreference
            {
                PushEnabled = dto.PushEnabled,
                MinimumSeverity = (NoticeSeverity)dto.MinimumSeverity,
                EmailEnabled = dto.EmailEnabled,
                EmailEventTypes = NotificationEventTypes.Join(dto.EmailEventTypes)
            };
        }
    }
}
