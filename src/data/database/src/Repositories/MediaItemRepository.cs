using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for MediaItem entities.</summary>
    public class MediaItemRepository(IUnitOfWork unitOfWork, ILogger<MediaItemRepository> logger) : IMediaItemRepository
    {
        /// <summary>Gets a media item by ID.</summary>
        public async Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct), ct);
        }

        /// <summary>Gets media items by device ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.Where(m => m.DeviceId == deviceId).ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> GetDistinctVideoResolutionsAsync(Guid deviceId, CancellationToken ct)
        {
            // Distinct strings only, so the set stays small; the largest is chosen by pixel count in the caller.
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => m.DeviceId == deviceId
                    && m.Resolution != null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .Select(m => m.Resolution!)
                .Distinct()
                .ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task<MediaItem?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.MediaFormat == "mp4"
                    && m.MetadataJson != null && m.MetadataJson.Contains("\"type\":\"snapshot-rollup\""))
                .OrderByDescending(m => m.RecordedAtUtc)
                .FirstOrDefaultAsync(ct), ct);
        }
        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .AsNoTracking()
                .Where(m => m.DeviceId == deviceId)
                .OrderByDescending(m => m.RecordedAtUtc)
                .Take(limit)
                .ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task<MediaItemPage> GetPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false)
        {
            // Bounds are checked here as well as at the endpoint: an unbounded take would turn the page back into a full scan.
            ArgumentOutOfRangeException.ThrowIfLessThan(skip, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 500);

            // Materialize the ids once so EF translates a plain IN list rather than re-enumerating the caller's collection.
            Guid[] ids = deviceIds.ToArray();

            return await unitOfWork.ReadAsync<MediaItemPage>(async context =>
            {
                IQueryable<MediaItem> matches = context.Db().MediaItems
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.DeviceId) && !m.IsPurged && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc);
                if (videoOnly)
                {
                    matches = matches.Where(m => m.MediaFormat != null && (m.MediaFormat.Contains("mp4") || m.MediaFormat.Contains("video")));
                }

                int total = await matches.CountAsync(ct);
                List<MediaItem> items = await matches
                    .OrderBy(m => m.RecordedAtUtc)
                    .ThenBy(m => m.Id)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(ct);

                return new MediaItemPage(items, total);
            }, ct);
        }

        /// <summary>Gets media items by device ID within a date range.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Oldest non-purged media with no video codec recorded. Rows the codec backfill has already tried and failed on are skipped by the caller.</summary>
        public async Task<IReadOnlyList<MediaItem>> ListMissingCodecAsync(int limit, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => !m.IsPurged && m.VideoCodec == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToListAsync(ct), ct);
        }

        /// <summary>Oldest media that analysis has never run on. Analysis sets BaselineVersion on every result, so null means not yet analysed.</summary>
        public async Task<IReadOnlyList<MediaItem>> ListUnanalyzedAsync(int limit, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => !m.IsPurged && m.BaselineVersion == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToListAsync(ct), ct);
        }

        /// <summary>
        /// Snapshot JPEGs of a device in a window, oldest first. Format, the snapshot marker, and the time filter all run in SQL.
        /// </summary>
        public async Task<IReadOnlyList<MediaItem>> GetSnapshotsByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => m.DeviceId == deviceId
                    && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc
                    && m.MediaFormat == "jpg"
                    && m.MetadataJson != null && m.MetadataJson.Contains("\"type\":\"snapshot\""))
                .OrderBy(m => m.RecordedAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>
        /// A device's stream-reported bitrates, oldest first. Estimated and missing bitrates are excluded, and so are time-lapse
        /// roll-ups: they are built from snapshots, so their bitrate is not a camera signal. Only the needed columns are read, all in SQL.
        /// </summary>
        public async Task<IReadOnlyList<MeasuredBitrate>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => m.DeviceId == deviceId && m.VideoBitrateBps != null && m.BitrateEstimated != true
                    && (m.MetadataJson == null || !m.MetadataJson.Contains("\"type\":\"snapshot-rollup\"")))
                .OrderBy(m => m.RecordedAtUtc)
                .Select(m => new MeasuredBitrate(m.RecordedAtUtc, m.VideoBitrateBps!.Value, m.LightingMode, m.RedFlags, m.Id))
                .ToListAsync(ct), ct);
        }

        /// <summary>
        /// A device's all-time bit rate baseline over the same rows as <see cref="GetMeasuredBitrateByDeviceAsync"/>, with no
        /// date window. The count, sum and sum of squares run as one SQL aggregate. The sums are cast to double, since SQLite
        /// sums of 64-bit integers overflow for squared bitrates. The mean and sample std dev are computed from those three
        /// values in C#, so only one scalar row is read.
        /// </summary>
        public async Task<AllTimeBitrateBaseline> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            var totals = await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .Where(m => m.DeviceId == deviceId && m.VideoBitrateBps != null && m.BitrateEstimated != true
                    && (m.MetadataJson == null || !m.MetadataJson.Contains("\"type\":\"snapshot-rollup\"")))
                .GroupBy(m => m.DeviceId)
                .Select(g => new
                {
                    Count = g.Count(),
                    Sum = g.Sum(m => (double)m.VideoBitrateBps!.Value),
                    SumOfSquares = g.Sum(m => (double)m.VideoBitrateBps!.Value * (double)m.VideoBitrateBps!.Value),
                })
                .FirstOrDefaultAsync(ct), ct);

            return totals is null
                ? AllTimeBitrateBaseline.FromTotals(0, 0, 0)
                : AllTimeBitrateBaseline.FromTotals(totals.Count, totals.Sum, totals.SumOfSquares);
        }

        /// <summary>Gets a media item by SHA-256 hash.</summary>
        public async Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.FirstOrDefaultAsync(m => m.Sha256Hash == sha256Hash, ct), ct);
        }

        /// <summary>
        /// Clips recorded since <paramref name="sinceUtc"/> whose RedFlags name BitrateCollapse, oldest first. The time and
        /// flag filters run in SQL, so only the matching rows are read.
        /// </summary>
        public async Task<IReadOnlyList<MediaItem>> ListFlaggedSinceAsync(DateTime sinceUtc, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .AsNoTracking()
                .Where(m => m.RecordedAtUtc >= sinceUtc && m.RedFlags != null && m.RedFlags.Contains("BitrateCollapse"))
                .OrderBy(m => m.RecordedAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Gets a media item by API source hash for deduplication.</summary>
        public async Task<MediaItem?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.FirstOrDefaultAsync(m => m.ApiSourceHash == apiSourceHash, ct), ct);
        }

        /// <summary>Gets media items by download event ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.Where(m => m.DownloadEventId == downloadEventId).ToListAsync(ct), ct);
        }

        /// <summary>Lists all media items.</summary>
        public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems.ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The video test mirrors the thumbnail service's rule (format name contains "video" or "mp4"), so the
        /// database returns only rows that the backfill would act on.
        /// </remarks>
        public async Task<IReadOnlyList<MediaItem>> ListVideosWithoutThumbnailAsync(CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .AsNoTracking()
                .Where(m => !m.IsPurged
                    && m.ThumbnailPath == null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Same video test as <see cref="ListVideosWithoutThumbnailAsync"/>. Oldest first, so a failing clip that stays
        /// without a preview is retried in the same order on every pass.
        /// </remarks>
        public async Task<IReadOnlyList<MediaItem>> ListVideosWithoutPreviewAsync(int limit, CancellationToken ct)
        {
            return await unitOfWork.ReadAsync(context => context.Db().MediaItems
                .AsNoTracking()
                .Where(m => !m.IsPurged
                    && m.PreviewPath == null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToListAsync(ct), ct);
        }

        /// <summary>Adds a new media item.</summary>
        public async Task AddAsync(MediaItem mediaItem, CancellationToken ct)
        {
            try
            {
                _ = await unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.MediaItems.Add(mediaItem);
                    _ = await db.SaveChangesAsync(ct);
                    // Guard: the sanitized name is only worth computing when debug logging is on.
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Media item added: {FileName}", SanitizeForLog(mediaItem.FileName));
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError("Error adding media item: {FileName}: {Message} ({ErrorType})", SanitizeForLog(mediaItem.FileName), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing media item.</summary>
        public async Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
        {
            try
            {
                _ = await unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.MediaItems.Update(mediaItem);
                    _ = await db.SaveChangesAsync(ct);
                    // Guard: the sanitized name is only worth computing when debug logging is on.
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Media item updated: {FileName}", SanitizeForLog(mediaItem.FileName));
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError("Error updating media item: {FileName}: {Message} ({ErrorType})", SanitizeForLog(mediaItem.FileName), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes a media item.</summary>
        public async Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            try
            {
                _ = await unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    MediaItem? mediaItem = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct);
                    if (mediaItem != null)
                    {
                        _ = db.MediaItems.Remove(mediaItem);
                        _ = await db.SaveChangesAsync(ct);
                        // Guard: keeps the debug log call off the hot path when debug logging is off.
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Media item deleted: {MediaItemId}", mediaItemId);
                        }
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError("Error deleting media item: {MediaItemId}: {Message} ({ErrorType})", mediaItemId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
