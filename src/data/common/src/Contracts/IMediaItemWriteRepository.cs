using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for media item entities.</summary>
    public interface IMediaItemWriteRepository
    {
        /// <summary>Gets a media item by ID.</summary>
        Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets media items by device ID.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>
        /// Distinct resolutions ("WIDTHxHEIGHT") of this device's video files. Used only as a fallback when no official spec is known.
        /// Default: no rows, for implementations that have no database behind them (the remote client).
        /// </summary>
        Task<IReadOnlyList<string>> GetDistinctVideoResolutionsAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <summary>
        /// The device's newest roll-up loop, or null when it has none. Read by marker in SQL, so an older loop still shows once
        /// snapshots have filled the recent-media window. Default: none, for implementations with no database behind them.
        /// </summary>
        Task<MediaItem?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult<MediaItem?>(null);
        }

        /// <summary>Gets a device's newest media items by recording time, at most <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct);

        /// <summary>
        /// Pages non-purged clips from any of the given devices within a UTC range, oldest first. Filtering, sorting, paging and
        /// counting all run in SQL. <paramref name="take"/> must be 1..500. When <paramref name="videoOnly"/> is true, only video
        /// rows (MediaFormat containing "mp4" or "video") are counted and returned.
        /// </summary>
        Task<MediaItemPage> GetPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false);

        /// <summary>Gets media items by device ID within a date range.</summary>
        Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Oldest media items that analysis has never run on (no baseline version yet), up to <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItem>> ListUnanalyzedAsync(int limit, CancellationToken ct);

        /// <summary>Oldest non-purged media items with no video codec recorded yet, up to <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItem>> ListMissingCodecAsync(int limit, CancellationToken ct);

        /// <summary>
        /// Clips recorded at or after <paramref name="sinceUtc"/> whose stored RedFlags contain BitrateCollapse, oldest first.
        /// Only the stored RedFlags string is read; analysis is not run again.
        /// </summary>
        Task<IReadOnlyList<MediaItem>> ListFlaggedSinceAsync(DateTime sinceUtc, CancellationToken ct);

        /// <summary>
        /// Snapshot JPEGs of a device in [fromUtc, toUtc], oldest first. The format and snapshot marker are filtered in the
        /// database. Files on disk are not checked here.
        /// </summary>
        Task<IReadOnlyList<MediaItem>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>
        /// A device's whole history of stream-reported video bitrates, oldest first. Estimated bitrates and clips with no
        /// bitrate are left out. Filtering, ordering and the projection run in SQL.
        /// </summary>
        Task<IReadOnlyList<MeasuredBitrate>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>
        /// A device's all-time bit rate baseline (count, mean and sample std dev of every stream-reported bitrate), with no date
        /// window. Uses the same rows as <see cref="GetMeasuredBitrateByDeviceAsync"/>. The aggregates run in SQL.
        /// </summary>
        Task<AllTimeBitrateBaseline> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct);

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

        /// <summary>
        /// Non-purged video items whose <see cref="MediaItem.PreviewPath"/> is null, oldest first by recording time, up to
        /// <paramref name="limit"/>. Filtering, sorting and paging run in SQL.
        /// </summary>
        Task<IReadOnlyList<MediaItem>> ListVideosWithoutPreviewAsync(int limit, CancellationToken ct);

        /// <summary>Adds a new media item.</summary>
        Task AddAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Updates an existing media item.</summary>
        Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);

        /// <summary>Deletes a media item.</summary>
        Task DeleteAsync(Guid mediaItemId, CancellationToken ct);
    }
}
