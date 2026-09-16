using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Persisted record of a notification event dispatched to live channels (email, SignalR, push). Mirrors what NotificationDispatcher fans out live (plan §5.6), enabling audit trail and historical view.</summary>
    public class Notice
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Event type (e.g. "CredentialDecryptionFailed").</summary>
        public required string EventType { get; set; }

        /// <summary>Severity level (Critical=account/credential failures, Alert=detection-type issues, Warning=other, Info=default).</summary>
        public NoticeSeverity Severity { get; set; } = NoticeSeverity.Info;

        /// <summary>Human-readable details (max 2000 chars).</summary>
        public string? Details { get; set; }

        /// <summary>Routing audience (0=All, 1=AdminsOnly). Stored as int to avoid data.common referencing providers-common's NotificationAudience enum; use comment to document mapping.</summary>
        public int Audience { get; set; } = 0; // 0=All, 1=AdminsOnly (mirrors NotificationAudience enum values)

        /// <summary>Creation timestamp UTC.</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>Optional FK to Operator - the operator context that triggered this notice, if any.</summary>
        public Guid? OperatorId { get; set; }

        /// <summary>Optional FK to ProviderAccount - e.g. which account had the credential decryption failure, for context.</summary>
        public Guid? ProviderAccountId { get; set; }
    }
}
