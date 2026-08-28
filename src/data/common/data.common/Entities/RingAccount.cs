namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Ring account-level data: subscription, features, account settings.</summary>
    public class RingAccount
    {
        public Guid Id { get; set; }
        public Guid ProviderAccountId { get; set; }
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
    }
}
