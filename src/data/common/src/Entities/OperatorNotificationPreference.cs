using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Per-operator push notification settings - one row per OperatorId (upsert semantics, like OperatorPreferences). Controls which severity levels trigger push notifications, reducing noise.</summary>
    public class OperatorNotificationPreference
    {
        /// <summary>FK to Operator (PK-like, one row per operator).</summary>
        public Guid OperatorId { get; set; }

        /// <summary>Whether push notifications are enabled for this operator.</summary>
        public bool PushEnabled { get; set; }

        /// <summary>Minimum severity level that triggers a push notification (default Info - all levels). Operators can choose Warning+ or Critical-only to reduce noise.</summary>
        public NoticeSeverity MinimumSeverity { get; set; } = NoticeSeverity.Info;

        /// <summary>Whether email notifications are enabled for this operator. Email is sent only for event types listed in <see cref="EmailEventTypes"/>.</summary>
        public bool EmailEnabled { get; set; }

        /// <summary>Comma-separated <c>SecurityAuditEventTypes</c> names that trigger an email. Empty means no email. Use <see cref="NotificationEventTypes.Join"/> to write it and <see cref="EmailEventTypeSet"/> to read it.</summary>
        public string EmailEventTypes { get; set; } = string.Empty;

        /// <summary>The parsed set of event type names in <see cref="EmailEventTypes"/> (exact, case-sensitive names; blanks ignored).</summary>
        public IReadOnlySet<string> EmailEventTypeSet => NotificationEventTypes.Parse(EmailEventTypes);
    }

    /// <summary>Reads and writes the comma-separated event type list stored in <see cref="OperatorNotificationPreference.EmailEventTypes"/>.</summary>
    public static class NotificationEventTypes
    {
        /// <summary>Parses a comma-separated list of event type names. Names are trimmed, blanks are ignored, and matching is case-sensitive and exact. Null or empty input returns an empty set.</summary>
        /// <param name="commaSeparated">The stored comma-separated list, or null.</param>
        /// <returns>A read-only set of the distinct names.</returns>
        public static IReadOnlySet<string> Parse(string? commaSeparated)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(commaSeparated))
            {
                return names;
            }

            foreach (string part in commaSeparated.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    _ = names.Add(name);
                }
            }

            return names;
        }

        /// <summary>Joins event type names into the comma-separated form stored in the database. Names are trimmed, blanks are dropped, and duplicates are removed, so <see cref="Parse"/> returns the same set.</summary>
        /// <param name="names">The event type names to store.</param>
        /// <returns>The comma-separated list, or an empty string when no names remain.</returns>
        public static string Join(IEnumerable<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);

            return string.Join(",", names
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.Ordinal));
        }
    }
}
