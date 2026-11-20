using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Finds the most recent action log entry, which is the tail of the hash chain.</summary>
    public static class ActionLogTail
    {
        /// <summary>
        /// The newest entry by timestamp. Appends always write a timestamp strictly later than the tail they read, so
        /// timestamps are unique along the chain and no tie-break is needed. Ordering by timestamp alone lets SQLite
        /// walk the TimestampUtc index for one row; adding an Id tie-break forces a full scan and a sort.
        /// </summary>
        public static IQueryable<ActionLogEntry> Latest(VideoForensicsDbContext db)
        {
            return db.ActionLogEntries
                .AsNoTracking()
                .OrderByDescending(ale => ale.TimestampUtc);
        }
    }
}
