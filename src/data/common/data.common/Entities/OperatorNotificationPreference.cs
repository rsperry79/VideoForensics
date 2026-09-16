using VideoForensics.Providers.Common.Contracts;

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
    }
}
