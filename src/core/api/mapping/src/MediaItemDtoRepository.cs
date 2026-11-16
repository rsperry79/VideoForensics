using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Server-side <see cref="IMediaItemRepository"/> that reads media through <see cref="IMediaItemWriteRepository"/> and
    /// maps each entity to its DTO. Gives the server the DTO read surface without a second store.
    /// </summary>
    public sealed class MediaItemDtoRepository(IMediaItemWriteRepository items) : IMediaItemRepository
    {
        /// <inheritdoc />
        public async Task<MediaItemDto?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            VideoForensics.Data.Common.Entities.MediaItem? item = await items.GetAsync(mediaItemId, ct);
            return item?.ToDto();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct) =>
            (await items.GetByDeviceIdAsync(deviceId, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> GetDistinctVideoResolutionsAsync(Guid deviceId, CancellationToken ct) =>
            items.GetDistinctVideoResolutionsAsync(deviceId, ct);

        /// <inheritdoc />
        public async Task<MediaItemDto?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct)
        {
            VideoForensics.Data.Common.Entities.MediaItem? item = await items.GetLatestRollupLoopAsync(deviceId, ct);
            return item?.ToDto();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct) =>
            (await items.GetRecentByDeviceAsync(deviceId, limit, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<MediaItemPageDto> GetPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false)
        {
            MediaItemPage page = await items.GetPageByDevicesAndDateRangeAsync(deviceIds, fromUtc, toUtc, skip, take, ct, videoOnly);
            return new MediaItemPageDto(page.Items.Select(i => i.ToDto()).ToList(), page.TotalCount);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await items.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await items.GetSnapshotsByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<MeasuredBitrateDto>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct) =>
            (await items.GetMeasuredBitrateByDeviceAsync(deviceId, ct)).Select(b => b.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<AllTimeBitrateBaselineDto> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            AllTimeBitrateBaseline baseline = await items.GetBitrateBaselineByDeviceAsync(deviceId, ct);
            return baseline.ToDto();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> ListAsync(CancellationToken ct) =>
            (await items.ListAsync(ct)).Select(i => i.ToDto()).ToList();
    }
}
