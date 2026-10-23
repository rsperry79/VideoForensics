using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for ProviderApiErrorLog rows — one Event can have many rows (one per failed attempt).</summary>
    public interface IProviderApiErrorLogRepository
    {
        /// <summary>Records a single failed attempt's error details.</summary>
        Task RecordAsync(ProviderApiErrorLog entry, CancellationToken ct);

        /// <summary>Gets every recorded failure for a given Event, in chronological order.</summary>
        Task<IReadOnlyList<ProviderApiErrorLog>> GetByEventIdAsync(Guid eventId, CancellationToken ct);

        /// <summary>Gets every recorded failure for a given device (used for snapshot failures, which have no EventId).</summary>
        Task<IReadOnlyList<ProviderApiErrorLog>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Deletes error rows older than <paramref name="age"/>. Returns how many were removed. Operational rows only; audit tables are not pruned.</summary>
        Task<int> PruneOlderThanAsync(TimeSpan age, CancellationToken ct);
    }
}
