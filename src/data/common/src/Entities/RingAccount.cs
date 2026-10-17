namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Ring account-level data: subscription, features, account settings.</summary>
    public class RingAccount
    {
        public Guid Id { get; set; }
        public Guid ProviderAccountId { get; set; }
        public Guid? RingAccountFeaturesId { get; set; }
        public required string SubscriptionLevel { get; set; }
        public string? Features { get; set; }
        public int? RateLimitPerMinute { get; set; }
        public int? RateLimitRemaining { get; set; }
        public string? AccountEmail { get; set; }
        public DateTime? AuthenticatedAtUtc { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ApiResponseHash { get; set; }
        public string? MetadataJson { get; set; }

        /// <summary>Hours between config syncs (profile, ringtones, light groups, location mode, device settings). Minimum 1.</summary>
        public int ConfigSyncIntervalHours { get; set; } = 24;

        /// <summary>UTC time the last config sync completed, or null if it has never run.</summary>
        public DateTime? LastConfigSyncUtc { get; set; }
    }
}
