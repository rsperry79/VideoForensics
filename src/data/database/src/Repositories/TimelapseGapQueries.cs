using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Read-only queries for the event-gap time-lapse planner.</summary>
    public sealed class TimelapseGapQueries : ITimelapseGapQueries
    {
        // Our own time-lapse events are excluded from gap boundaries, so the planner never plans around its own output.
        private const string TimelapseEventType = "timelapse";
        private const string LoopMarker = "\"type\":\"snapshot-rollup\"";

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<TimelapseGapQueries> _logger;

        public TimelapseGapQueries(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<TimelapseGapQueries> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DateTime>> ListEventTimesForGapsAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            List<DateTime> times = await db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId
                    && e.EventType != TimelapseEventType
                    && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .OrderBy(e => e.OccurredAtUtc)
                .Select(e => e.OccurredAtUtc)
                .ToListAsync(ct);

            // SQLite returns unspecified kind; the planner works in UTC.
            return times.Select(t => DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToList();
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> GetEarliestCaptureAtUtcAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.SnapshotCaptures.AsNoTracking()
                .Where(c => c.DeviceId == deviceId)
                .OrderBy(c => c.CapturedAtUtc)
                .Select(c => (DateTimeOffset?)c.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        /// <inheritdoc />
        public async Task<DateTime?> GetLatestTimelapseEndUtcAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            var latest = await db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.MetadataJson != null && m.MetadataJson.Contains(LoopMarker))
                .OrderByDescending(m => m.RecordedAtUtc)
                .Select(m => new { m.RecordedAtUtc, m.DurationSeconds })
                .FirstOrDefaultAsync(ct);

            if (latest is null)
            {
                return null;
            }

            return DateTime.SpecifyKind(latest.RecordedAtUtc.AddSeconds(latest.DurationSeconds ?? 0), DateTimeKind.Utc);
        }
    }
}
