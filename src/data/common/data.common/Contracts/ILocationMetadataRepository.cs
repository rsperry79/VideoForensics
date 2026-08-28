using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for location metadata (address, timezone, coordinates).</summary>
    public interface ILocationMetadataRepository
    {
        /// <summary>Gets metadata by ID.</summary>
        Task<LocationMetadata?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets metadata for a location.</summary>
        Task<LocationMetadata?> GetByLocationIdAsync(Guid locationId, CancellationToken ct);

        /// <summary>Adds new metadata record.</summary>
        Task AddAsync(LocationMetadata metadata, CancellationToken ct);

        /// <summary>Updates existing metadata.</summary>
        Task UpdateAsync(LocationMetadata metadata, CancellationToken ct);

        /// <summary>Deletes metadata record.</summary>
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
