using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Media queries shared by the transactional UnitOfWork repository and the standalone MediaItemRepository. Each returns an
    /// unmaterialized query, so each caller decides how to run it (sync inside a transaction, or async on its own context).
    /// Why: the two paths used to repeat the same predicates, and they could drift apart.
    /// </summary>
    internal static class MediaItemQueries
    {
        /// <summary>The device's newest time-lapse loop: an mp4 with the Timelapse kind, newest first.</summary>
        internal static IQueryable<MediaItem> LatestRollupLoop(VideoForensicsDbContext db, Guid deviceId)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.MediaFormat == "mp4"
                    && m.MediaKind == MediaKinds.Timelapse)
                .OrderByDescending(m => m.RecordedAtUtc);
        }

        /// <summary>Every media row of a device, untracked.</summary>
        internal static IQueryable<MediaItem> ByDevice(VideoForensicsDbContext db, Guid deviceId)
        {
            return db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId);
        }

        /// <summary>A device's newest media rows, at most <paramref name="limit"/>.</summary>
        internal static IQueryable<MediaItem> RecentByDevice(VideoForensicsDbContext db, Guid deviceId, int limit)
        {
            return ByDevice(db, deviceId).OrderByDescending(m => m.RecordedAtUtc).Take(limit);
        }

        /// <summary>Snapshot JPEGs of a device in a UTC window, oldest first.</summary>
        internal static IQueryable<MediaItem> SnapshotsByDeviceAndDateRange(VideoForensicsDbContext db, Guid deviceId, DateTime fromUtc, DateTime toUtc)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc
                    && m.MediaFormat == "jpg" && m.MediaKind == MediaKinds.Snapshot)
                .OrderBy(m => m.RecordedAtUtc);
        }

        /// <summary>A device's media rows recorded in a UTC window, untracked, unordered.</summary>
        internal static IQueryable<MediaItem> ByDeviceAndDateRange(VideoForensicsDbContext db, Guid deviceId, DateTime fromUtc, DateTime toUtc)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc);
        }

        /// <summary>Clips recorded at or after <paramref name="sinceUtc"/> whose stored RedFlags name BitrateCollapse, oldest first.</summary>
        internal static IQueryable<MediaItem> FlaggedSince(VideoForensicsDbContext db, DateTime sinceUtc)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => m.RecordedAtUtc >= sinceUtc && m.RedFlags != null && m.RedFlags.Contains("BitrateCollapse"))
                .OrderBy(m => m.RecordedAtUtc);
        }

        /// <summary>Oldest non-purged media with no video codec recorded, at most <paramref name="limit"/>.</summary>
        internal static IQueryable<MediaItem> MissingCodec(VideoForensicsDbContext db, int limit)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.VideoCodec == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit);
        }

        /// <summary>Oldest non-purged media that analysis has never run on, at most <paramref name="limit"/>.</summary>
        internal static IQueryable<MediaItem> Unanalyzed(VideoForensicsDbContext db, int limit)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.BaselineVersion == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit);
        }

        /// <summary>Every media row written by one download event, untracked.</summary>
        internal static IQueryable<MediaItem> ByDownloadEventId(VideoForensicsDbContext db, Guid downloadEventId)
        {
            return db.MediaItems.AsNoTracking().Where(m => m.DownloadEventId == downloadEventId);
        }

        /// <summary>The media row with this id, or none.</summary>
        internal static IQueryable<MediaItem> ById(VideoForensicsDbContext db, Guid mediaItemId)
        {
            return db.MediaItems.AsNoTracking().Where(m => m.Id == mediaItemId);
        }

        /// <summary>Non-purged video rows that have no thumbnail yet.</summary>
        internal static IQueryable<MediaItem> VideosWithoutThumbnail(VideoForensicsDbContext db)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.ThumbnailPath == null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")));
        }

        /// <summary>Oldest non-purged video rows that have no preview clip yet, at most <paramref name="limit"/>.</summary>
        internal static IQueryable<MediaItem> VideosWithoutPreview(VideoForensicsDbContext db, int limit)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.PreviewPath == null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit);
        }

        /// <summary>
        /// A device's stream-reported bitrates, oldest first. Estimated bitrates and time-lapse roll-ups are excluded.
        /// Why: roll-ups are built from snapshots, so their bitrate is not a camera signal.
        /// </summary>
        internal static IQueryable<MeasuredBitrate> MeasuredBitrates(VideoForensicsDbContext db, Guid deviceId)
        {
            return db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.VideoBitrateBps != null && m.BitrateEstimated != true
                    && m.MediaKind != MediaKinds.Timelapse)
                .OrderBy(m => m.RecordedAtUtc)
                .Select(m => new MeasuredBitrate(m.RecordedAtUtc, m.VideoBitrateBps!.Value, m.LightingMode, m.RedFlags, m.Id));
        }

        /// <summary>The media row with this SHA-256, or none.</summary>
        internal static IQueryable<MediaItem> ByHash(VideoForensicsDbContext db, string sha256Hash)
        {
            return db.MediaItems.AsNoTracking().Where(m => m.Sha256Hash == sha256Hash);
        }

        /// <summary>The media row with this API source hash, or none.</summary>
        internal static IQueryable<MediaItem> ByApiSourceHash(VideoForensicsDbContext db, string apiSourceHash)
        {
            return db.MediaItems.AsNoTracking().Where(m => m.ApiSourceHash == apiSourceHash);
        }
    }
}
