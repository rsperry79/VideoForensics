using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository for timeline analysis and forensic gap detection.</summary>
    public class TimelineRepository : ITimelineRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<TimelineRepository> _logger;
        private readonly IEventRepository _eventRepository;
        private readonly IDeviceRepository _deviceRepository;

        public TimelineRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<TimelineRepository> logger,
            IEventRepository eventRepository,
            IDeviceRepository deviceRepository)
        {
            _factory = factory;
            _logger = logger;
            _eventRepository = eventRepository;
            _deviceRepository = deviceRepository;
        }

        public async Task<IReadOnlyList<TimelineGap>> GetRecordingGapsAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var events = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);

            var gaps = new List<TimelineGap>();
            for (int i = 0; i < events.Count - 1; i++)
            {
                var gapDuration = (events[i + 1].OccurredAtUtc - events[i].OccurredAtUtc).TotalMinutes;
                if (gapDuration >= minGapMinutes)
                {
                    var device = await _deviceRepository.GetAsync(deviceId, ct);
                    gaps.Add(new TimelineGap
                    {
                        DeviceId = deviceId,
                        DeviceName = device?.Name ?? "Unknown",
                        StartUtc = events[i].OccurredAtUtc,
                        EndUtc = events[i + 1].OccurredAtUtc,
                        DurationMinutes = (int)gapDuration,
                        EventsBeforeGap = i + 1,
                        EventsAfterGap = events.Count - i - 1
                    });
                }
            }

            return gaps;
        }

        public async Task<IReadOnlyList<TimelineGap>> GetLocationRecordingGapsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices
                .Where(d => d.LocationId == locationId)
                .Select(d => d.Id)
                .ToListAsync(ct);

            var allGaps = new List<TimelineGap>();
            foreach (var deviceId in devices)
            {
                var gaps = await GetRecordingGapsAsync(deviceId, fromUtc, toUtc, minGapMinutes, ct);
                allGaps.AddRange(gaps);
            }

            return allGaps.OrderBy(g => g.StartUtc).ToList();
        }

        public async Task<Dictionary<int, int>> GetEventCountByHourAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var events = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);

            return events
                .GroupBy(e => e.OccurredAtUtc.Hour)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<Dictionary<string, int>> GetEventCountByDayAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var events = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);

            return events
                .GroupBy(e => e.OccurredAtUtc.Date.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<IReadOnlyList<(int Hour, int Count)>> GetPeakActivityPeriodsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var hourly = await GetEventCountByHourAsync(Guid.Empty, fromUtc, toUtc, ct);
            return hourly
                .OrderByDescending(h => h.Value)
                .Select(h => (h.Key, h.Value))
                .ToList();
        }

        public async Task<TimelineIntegrityReport> VerifyTimelineIntegrityAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var gaps = await GetLocationRecordingGapsAsync(locationId, fromUtc, toUtc, minGapMinutes: 5, ct);

            await using var db = await _factory.CreateDbContextAsync(ct);
            var allEvents = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);

            var totalDurationMinutes = (toUtc - fromUtc).TotalMinutes;
            var gappedDurationMinutes = gaps.Sum(g => g.DurationMinutes);
            var coverage = ((totalDurationMinutes - gappedDurationMinutes) / totalDurationMinutes) * 100m;

            var report = new TimelineIntegrityReport
            {
                LocationId = locationId,
                AnalysisFromUtc = fromUtc,
                AnalysisToUtc = toUtc,
                TotalEvents = allEvents.Count,
                TotalGaps = gaps.Count,
                LargestGapMinutes = gaps.Count > 0 ? gaps.Max(g => g.DurationMinutes) : 0,
                CoveragePercentage = (decimal)coverage,
                SignificantGaps = gaps.Where(g => g.DurationMinutes >= 30).ToList(),
                EventTypeDistribution = allEvents
                    .GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                IntegrityStatus = coverage < 50m ? "Critical" : coverage < 80m ? "Gaps" : "Intact"
            };

            _logger.LogInformation(
                "Timeline integrity: {LocationId} coverage {Coverage:F1}% ({TotalEvents} events, {Gaps} gaps)",
                locationId, coverage, allEvents.Count, gaps.Count);

            return report;
        }
    }
}
