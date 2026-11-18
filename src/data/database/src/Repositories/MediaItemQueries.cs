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
    }
}
