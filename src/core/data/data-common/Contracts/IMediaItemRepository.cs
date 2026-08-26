using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for media item entities.</summary>
    public interface IMediaItemRepository
    {
        /// <summary>Gets a media item by ID.</summary>
        Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets media items by device ID.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets media items by device ID within a date range.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets a media item by SHA-256 hash.</summary>
        Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct);

        /// <summary>Gets media items by download event ID.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct);

        /// <summary>Lists all media items.</summary>
        Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);

        /// <summary>Adds a new media item.</summary>
        Task AddAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Updates an existing media item.</summary>
        Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Deletes a media item.</summary>
        Task DeleteAsync(Guid mediaItemId, CancellationToken ct);
    }
}
