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

        /// <summary>Lists every enabled sync schedule (IsEnabled only), including JammingWindows, ordered by ProviderAccountId so scheduler passes are deterministic.</summary>
        Task<IReadOnlyList<SyncSchedule>> ListEnabledAsync(CancellationToken ct);

        /// <summary>Records that an event poll ran: updates ONLY EventLastRunUtc and EventNextRunUtc of the matching row,
        /// never intervals or IsEnabled (the user may have edited them while the run was in flight). No-op if the row no longer exists.</summary>
        Task RecordEventRunAsync(Guid providerAccountId, DateTime ranAtUtc, DateTime nextRunUtc, CancellationToken ct);

        /// <summary>Records that a snapshot/RSSI poll ran: updates ONLY SnapshotLastRunUtc and SnapshotNextRunUtc of the matching row,
        /// never intervals or IsEnabled. No-op if the row no longer exists.</summary>
        Task RecordSnapshotRunAsync(Guid providerAccountId, DateTime ranAtUtc, DateTime nextRunUtc, CancellationToken ct);
    }
}
