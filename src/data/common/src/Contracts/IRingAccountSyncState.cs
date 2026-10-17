namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>The config-sync schedule for one Ring account, read by the sync gate.</summary>
    /// <param name="IntervalHours">Hours that must pass after <paramref name="LastConfigSyncUtc"/> before config runs again. Never below 1.</param>
    /// <param name="LastConfigSyncUtc">UTC time the last config sync completed, or null if it has never completed.</param>
    public sealed record RingConfigSyncState(int IntervalHours, DateTime? LastConfigSyncUtc);

    /// <summary>
    /// Reads and records the config-sync gate state for a Ring account, keyed by the provider account id.
    /// </summary>
    public interface IRingAccountSyncState
    {
        /// <summary>Returns the account's interval and last completed config sync. An unknown account reads as the default interval and never synced.</summary>
        Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Records that a config sync completed at <paramref name="utcNow"/>. A no-op when the account row does not exist.</summary>
        Task MarkConfigSyncCompletedAsync(Guid providerAccountId, DateTime utcNow, CancellationToken ct);
    }
}
