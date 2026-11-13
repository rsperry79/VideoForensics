using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Read-only queries for the event-gap time-lapse planner.</summary>
    public sealed class TimelapseGapQueries : ITimelapseGapQueries
    {
        // Our own time-lapse events are excluded from gap boundaries, so the planner never plans around its own output.
        private const string TimelapseEventType = "timelapse";
        private const string LoopMarker = "\"type\":\"snapshot-rollup\"";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TimelapseGapQueries> _logger;

        public TimelapseGapQueries(IUnitOfWork unitOfWork, ILogger<TimelapseGapQueries> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DateTime>> ListEventTimesForGapsAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            List<DateTime> times = await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId
                    && e.EventType != TimelapseEventType
                    && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .OrderBy(e => e.OccurredAtUtc)
                .Select(e => e.OccurredAtUtc)
                .ToListAsync(ct), ct);

            // SQLite returns unspecified kind; the planner works in UTC.
            return times.Select(t => DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToList();
        }

        /// <inheritdoc />
        public async Task<DateTime?> GetLatestEventTimeUtcAsync(Guid deviceId, DateTime upToUtc, CancellationToken ct)
        {
            DateTime? latest = await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.EventType != TimelapseEventType && e.OccurredAtUtc <= upToUtc)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Select(e => (DateTime?)e.OccurredAtUtc)
                .FirstOrDefaultAsync(ct), ct);

            return latest is null ? null : DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc);
        }

        /// <inheritdoc />
        public async Task<DateTime?> GetLatestEventTimeAnyUtcAsync(Guid deviceId, CancellationToken ct)
        {
            DateTime? latest = await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.EventType != TimelapseEventType)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Select(e => (DateTime?)e.OccurredAtUtc)
                .FirstOrDefaultAsync(ct), ct);

            return latest is null ? null : DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc);
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> GetEarliestCaptureAtUtcAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().SnapshotCaptures.AsNoTracking()
                .Where(c => c.DeviceId == deviceId)
                .OrderBy(c => c.CapturedAtUtc)
                .Select(c => (DateTimeOffset?)c.CapturedAtUtc)
                .FirstOrDefaultAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task<DateTime?> GetLatestTimelapseEndUtcAsync(Guid deviceId, CancellationToken ct)
        {
            var latest = await _unitOfWork.ReadAsync(context => context.Db().MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.MetadataJson != null && m.MetadataJson.Contains(LoopMarker))
                .OrderByDescending(m => m.RecordedAtUtc)
                .Select(m => new { m.RecordedAtUtc, m.DurationSeconds })
                .FirstOrDefaultAsync(ct), ct);

            if (latest is null)
            {
                return null;
            }

            return DateTime.SpecifyKind(latest.RecordedAtUtc.AddSeconds(latest.DurationSeconds ?? 0), DateTimeKind.Utc);
        }
    }
}
