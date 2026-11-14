namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Read access to media items, with DTO signatures. Client hosts use this interface. The server implements it over
    /// the database (see DeviceDtoRepository's sibling in the mapping project). Writes and entity-only queries are
    /// server-only (IMediaItemWriteRepository).
    /// </summary>
    public interface IMediaItemRepository
    {
        /// <summary>Gets a media item by ID, or null.</summary>
        Task<MediaItemDto?> GetAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets media items by device ID.</summary>
        Task<IReadOnlyList<MediaItemDto>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Distinct resolutions of a device's video files. Empty for implementations with no database behind them.</summary>
        Task<IReadOnlyList<string>> GetDistinctVideoResolutionsAsync(Guid deviceId, CancellationToken ct);

        /// <summary>The device's newest roll-up loop, or null.</summary>
        Task<MediaItemDto?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct);

        /// <summary>A device's newest media items by recording time, at most <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<MediaItemDto>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct);

        /// <summary>Pages non-purged clips from any of the given devices within a UTC range, oldest first.</summary>
        Task<MediaItemPageDto> GetPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false);

        /// <summary>Gets media items by device ID within a date range.</summary>
        Task<IReadOnlyList<MediaItemDto>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Snapshot JPEGs of a device in [fromUtc, toUtc], oldest first.</summary>
        Task<IReadOnlyList<MediaItemDto>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>A device's whole history of stream-reported video bitrates, oldest first.</summary>
        Task<IReadOnlyList<MeasuredBitrateDto>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>A device's all-time bit rate baseline.</summary>
        Task<AllTimeBitrateBaselineDto> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all media items.</summary>
        Task<IReadOnlyList<MediaItemDto>> ListAsync(CancellationToken ct);
    }
}
