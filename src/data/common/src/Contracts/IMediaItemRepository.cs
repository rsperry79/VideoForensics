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

        /// <summary>Gets a device's newest media items by recording time, at most <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct);

        /// <summary>Gets media items by device ID within a date range.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Oldest media items that analysis has never run on (no baseline version yet), up to <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItem>> ListUnanalyzedAsync(int limit, CancellationToken ct);

        /// <summary>
        /// Snapshot JPEGs of a device in [fromUtc, toUtc], oldest first. The format and snapshot marker are filtered in the
        /// database. Files on disk are not checked here.
        /// </summary>
        Task<IReadOnlyList<MediaItem>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets a media item by SHA-256 hash.</summary>
        Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct);

        /// <summary>Gets a media item by API source hash for deduplication.</summary>
        Task<MediaItem?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct);

        /// <summary>Gets media items by download event ID.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct);

        /// <summary>Lists all media items.</summary>
        Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct);

        /// <summary>Lists non-purged video items whose <see cref="MediaItem.ThumbnailPath"/> is not yet set.</summary>
        Task<IReadOnlyList<MediaItem>> ListVideosWithoutThumbnailAsync(CancellationToken ct);

        /// <summary>Adds a new media item.</summary>
        Task AddAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Updates an existing media item.</summary>
        Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Deletes a media item.</summary>
        Task DeleteAsync(Guid mediaItemId, CancellationToken ct);
    }
}
