using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for append-only media integrity verification records.</summary>
    public interface IIntegrityRecordRepository
    {
        /// <summary>Adds a new integrity record.</summary>
        Task AddAsync(IntegrityRecord record, CancellationToken ct);

        /// <summary>Gets the most recent integrity record for each of the given media items.</summary>
        Task<IReadOnlyList<IntegrityRecord>> GetLatestByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct);
    }
}
