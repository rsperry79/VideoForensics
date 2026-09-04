namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for ProviderApiCallRecord rows backing the provider API budget guard (plan §5.12).</summary>
    public interface IProviderApiCallLogRepository
    {
        Task RecordCallAsync(string providerName, CancellationToken ct);
        Task<int> CountRecentCallsAsync(string providerName, TimeSpan window, CancellationToken ct);

        /// <summary>Deletes records older than the retention window - called opportunistically so this table doesn't grow unbounded.</summary>
        Task PruneOlderThanAsync(TimeSpan retain, CancellationToken ct);
    }
}
