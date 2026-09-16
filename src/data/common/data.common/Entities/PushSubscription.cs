namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Web Push API subscription credentials (VAPID-based) - one row per browser/device an operator has registered for push notifications.</summary>
    public class PushSubscription
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>FK to Operator - the operator who registered this subscription.</summary>
        public required Guid OperatorId { get; set; }

        /// <summary>Web Push subscription endpoint URL (globally unique per browser/device registration, UNIQUE index).</summary>
        public required string Endpoint { get; set; } // max 500

        /// <summary>VAPID P256dh key credential.</summary>
        public required string P256dhKey { get; set; } // max 500

        /// <summary>VAPID auth key credential.</summary>
        public required string AuthKey { get; set; } // max 500

        /// <summary>When this subscription was registered.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>When this subscription was last used to send a push (null if never used).</summary>
        public DateTime? LastUsedUtc { get; set; }
    }
}
