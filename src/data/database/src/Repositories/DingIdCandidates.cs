using Microsoft.EntityFrameworkCore;

using System.Globalization;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Device-scoped string ding id candidates for a doorbot history id. Shared by the download link path and the
    /// repair service so both resolve candidates the same way.
    /// </summary>
    public static class DingIdCandidates
    {
        /// <summary>
        /// Returns the ordered, distinct string ding ids of the device whose provider id is the doorbot id. Candidates
        /// come only from the string-id feeds (location events and video search), because those feeds store the string
        /// ding id that a doorbot history id must equal. Returns an empty list when no device has that doorbot id.
        /// </summary>
        /// <param name="db">The context to read from. Reads are untracked.</param>
        /// <param name="doorbotId">The numeric doorbot id of the device.</param>
        public static List<string> ForDoorbot(VideoForensicsDbContext db, long doorbotId)
        {
            ArgumentNullException.ThrowIfNull(db);

            // Resolve the device first. Without one, a matching doorbot id on a feed row would have no device to scope it.
            string providerDeviceId = doorbotId.ToString(CultureInfo.InvariantCulture);
            if (!db.Devices.AsNoTracking().Any(d => d.ProviderDeviceId == providerDeviceId))
            {
                return [];
            }

            var locationDings = db.RingLocationEvents.AsNoTracking()
                .Where(e => e.DoorbotId == doorbotId && e.DingIdString != null)
                .Select(e => e.DingIdString!)
                .ToList();
            // Why: start from this doorbot's links and join to the video rows. A correlated Any over every video row
            // scanned the whole table, which took seconds while a write held the database.
            var searchDings = (from x in db.RingEventDoorbots.AsNoTracking()
                               join v in db.RingVideoSearchEvents.AsNoTracking() on x.SourceRowId equals v.Id
                               where x.Source == RingEventSources.VideoSearch && x.DoorbotId == doorbotId && v.ProviderEventId != null
                               select v.ProviderEventId!)
                .ToList();

            return locationDings.Concat(searchDings).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        }
    }
}
