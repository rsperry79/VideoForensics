using Microsoft.EntityFrameworkCore;

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
