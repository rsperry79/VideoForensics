using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for ProviderSyncRun rows: one per account sync attempt.</summary>
    public interface IProviderSyncRunRepository
    {
        /// <summary>Records a completed sync attempt.</summary>
        Task AddAsync(ProviderSyncRun run, CancellationToken ct);

        /// <summary>Lists every sync run for an account, newest StartedUtc first.</summary>
        Task<IReadOnlyList<ProviderSyncRun>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Gets a single sync run by ID, or null if none exists.</summary>
        Task<ProviderSyncRun?> GetAsync(Guid id, CancellationToken ct);
    }
}
