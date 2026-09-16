using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for sync schedule entities.</summary>
    public interface ISyncScheduleRepository
    {
        /// <summary>Gets a sync schedule by provider account ID (includes JammingWindows navigation property).</summary>
        Task<SyncSchedule?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Adds or updates a sync schedule.</summary>
        Task UpsertAsync(SyncSchedule schedule, CancellationToken ct);

        /// <summary>Deletes a sync schedule by provider account ID.</summary>
        Task DeleteAsync(Guid providerAccountId, CancellationToken ct);
    }
}
