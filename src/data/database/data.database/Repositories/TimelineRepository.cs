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
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
            var deviceName = device?.Name ?? "Unknown";

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
                if (gapDuration > minGapMinutes)
                {
                    gaps.Add(new TimelineGap
                    {
                        DeviceId = deviceId,
                        DeviceName = deviceName,
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
            var deviceMap = await db.Devices
                .Where(d => d.LocationId == locationId)
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken: ct);

            var allEvents = await db.Events
                .Join(db.Devices.Where(d => d.LocationId == locationId),
                      e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Event.OccurredAtUtc >= fromUtc && x.Event.OccurredAtUtc <= toUtc)
                .OrderBy(x => x.Event.DeviceId)
                .ThenBy(x => x.Event.OccurredAtUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);

            var allGaps = new List<TimelineGap>();
            var eventsByDevice = allEvents.GroupBy(e => e.DeviceId);

            foreach (var deviceEvents in eventsByDevice)
            {
                var events = deviceEvents.OrderBy(e => e.OccurredAtUtc).ToList();
                for (int i = 0; i < events.Count - 1; i++)
                {
                    var gapDuration = (events[i + 1].OccurredAtUtc - events[i].OccurredAtUtc).TotalMinutes;
                    if (gapDuration > minGapMinutes)
                    {
                        allGaps.Add(new TimelineGap
                        {
                            DeviceId = deviceEvents.Key,
                            DeviceName = deviceMap.TryGetValue(deviceEvents.Key, out var name) ? name : "Unknown",
                            StartUtc = events[i].OccurredAtUtc,
                            EndUtc = events[i + 1].OccurredAtUtc,
                            DurationMinutes = (int)gapDuration,
                            EventsBeforeGap = i + 1,
                            EventsAfterGap = events.Count - i - 1
                        });
                    }
                }
            }

            return allGaps.OrderBy(g => g.StartUtc).ToList();
        }

        public async Task<Dictionary<int, int>> GetEventCountByHourAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .GroupBy(e => e.OccurredAtUtc.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Hour, x => x.Count, cancellationToken: ct);
        }

        public async Task<Dictionary<string, int>> GetEventCountByDayAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var byDate = await db.Events
                .Join(db.Devices.Where(d => d.LocationId == locationId),
                      e => e.DeviceId, d => d.Id, (e, d) => e)
                .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .GroupBy(e => e.OccurredAtUtc.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken: ct);

            return byDate.ToDictionary(x => x.Key.ToString("yyyy-MM-dd"), x => x.Value);
        }

        public async Task<IReadOnlyList<HourlyActivityCount>> GetPeakActivityPeriodsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var results = await db.Events
                .Join(db.Devices.Where(d => d.LocationId == locationId),
                      e => e.DeviceId, d => d.Id, (e, d) => e)
                .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .GroupBy(e => e.OccurredAtUtc.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            return results.Select(x => new HourlyActivityCount { Hour = x.Hour, Count = x.Count }).ToList();
        }

        public async Task<TimelineIntegrityReport> VerifyTimelineIntegrityAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var gaps = await GetLocationRecordingGapsAsync(locationId, fromUtc, toUtc, minGapMinutes: 5, ct);

            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
            var allEvents = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);

            var totalDurationMinutes = (decimal)(toUtc - fromUtc).TotalMinutes;
            var eventsByDevice = allEvents.ToLookup(e => e.DeviceId);
            var gapsByDevice = gaps.ToLookup(g => g.DeviceId);

            // Include every device at the location even with zero events - a fully-silent camera
            // is itself forensically significant and must not just vanish from the report.
            var deviceReports = devices.Select(device =>
            {
                var deviceEvents = eventsByDevice[device.Id].ToList();
                var deviceGaps = gapsByDevice[device.Id].ToList();
                var deviceGappedMinutes = (decimal)deviceGaps.Sum(g => g.DurationMinutes);
                var deviceCoverage = totalDurationMinutes > 0
                    ? ((totalDurationMinutes - deviceGappedMinutes) / totalDurationMinutes) * 100m
                    : 100m;

                return new DeviceTimelineIntegrity
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    TotalEvents = deviceEvents.Count,
                    TotalGaps = deviceGaps.Count,
                    LargestGapMinutes = deviceGaps.Count > 0 ? deviceGaps.Max(g => g.DurationMinutes) : 0,
                    CoveragePercentage = deviceCoverage,
                    SignificantGaps = deviceGaps.Where(g => g.DurationMinutes >= 30).ToList(),
                    EventTypeDistribution = deviceEvents
                        .GroupBy(e => e.EventType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    IntegrityStatus = deviceCoverage < 50m ? "Critical" : deviceCoverage < 80m ? "Gaps" : "Intact"
                };
            }).ToList();

            var report = new TimelineIntegrityReport
            {
                LocationId = locationId,
                AnalysisFromUtc = fromUtc,
                AnalysisToUtc = toUtc,
                DeviceReports = deviceReports
            };

            _logger.LogInformation(
                "Timeline integrity: {LocationId}, {DeviceCount} device(s), coverage {Coverages}",
                locationId, deviceReports.Count,
                string.Join(", ", deviceReports.Select(d => $"{d.DeviceName}={d.CoveragePercentage:F1}%")));

            return report;
        }

        public async Task<IReadOnlyList<CoordinatedEventCluster>> GetCoordinatedEventsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, int timeWindowSeconds, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var events = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => new { x.Event, x.Device })
                .OrderBy(x => x.Event.OccurredAtUtc)
                .ToListAsync(ct);

            var clusters = new List<CoordinatedEventCluster>();
            var processed = new HashSet<int>();

            for (int i = 0; i < events.Count; i++)
            {
                if (processed.Contains(i)) continue;

                var cluster = new CoordinatedEventCluster
                {
                    ClusterTimeUtc = events[i].Event.OccurredAtUtc
                };

                var devicesInCluster = new HashSet<Guid>();
                var timeWindow = new TimeSpan(0, 0, timeWindowSeconds);

                for (int j = i; j < events.Count; j++)
                {
                    if (events[j].Event.OccurredAtUtc - events[i].Event.OccurredAtUtc <= timeWindow)
                    {
                        cluster.Events.Add(new ClusterEvent
                        {
                            DeviceId = events[j].Device.Id,
                            DeviceName = events[j].Device.Name,
                            EventType = events[j].Event.EventType,
                            OccurredAtUtc = events[j].Event.OccurredAtUtc
                        });
                        devicesInCluster.Add(events[j].Device.Id);
                        processed.Add(j);
                    }
                    else
                    {
                        break;
                    }
                }

                cluster.DeviceCount = devicesInCluster.Count;
                cluster.TotalEventCount = cluster.Events.Count;

                // Only report clusters with multiple devices
                if (cluster.DeviceCount > 1)
                {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        public async Task<IReadOnlyList<SuspiciousActivityFlag>> FindSuspiciousCoordinatedActivityAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var flags = new List<SuspiciousActivityFlag>();
            var clusters = await GetCoordinatedEventsAsync(locationId, fromUtc, toUtc, timeWindowSeconds: 10, ct);

            foreach (var cluster in clusters)
            {
                var motionEvents = cluster.Events.Where(e => e.EventType.Contains("motion", StringComparison.OrdinalIgnoreCase)).ToList();
                if (motionEvents.Count > 0 && cluster.DeviceCount > 1)
                {
                    flags.Add(new SuspiciousActivityFlag
                    {
                        LocationId = locationId,
                        OccurredAtUtc = cluster.ClusterTimeUtc,
                        ActivityType = "SimultaneousMotion",
                        Description = $"Multiple devices ({cluster.DeviceCount}) detected motion simultaneously within 10 seconds",
                        SuspicionScore = Math.Min(100, cluster.DeviceCount * 25),
                        InvolvedDevices = cluster.Events
                            .Select(e => new InvolvedDevice { DeviceId = e.DeviceId, DeviceName = e.DeviceName })
                            .DistinctBy(d => d.DeviceId)
                            .ToList()
                    });
                }
            }

            return flags;
        }

        public async Task<TimelineSummary> GetTimelineSummaryAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var gaps = await GetLocationRecordingGapsAsync(locationId, fromUtc, toUtc, minGapMinutes: 5, ct);

            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
            var allEvents = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);

            var hourly = allEvents
                .GroupBy(e => e.OccurredAtUtc.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            var peakHours = hourly
                .OrderByDescending(h => h.Value)
                .Take(5)
                .Select(h => new HourlyActivityCount { Hour = h.Key, Count = h.Value })
                .ToList();

            var totalDurationMinutes = (decimal)(toUtc - fromUtc).TotalMinutes;
            var gapsByDevice = gaps.ToLookup(g => g.DeviceId);

            var deviceSummaries = devices.Select(device =>
            {
                var deviceGaps = gapsByDevice[device.Id].ToList();
                var deviceGappedMinutes = (decimal)deviceGaps.Sum(g => g.DurationMinutes);
                var deviceCoverage = totalDurationMinutes > 0
                    ? ((totalDurationMinutes - deviceGappedMinutes) / totalDurationMinutes) * 100m
                    : 100m;

                return new DeviceTimelineSummary
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    GapCount = deviceGaps.Count,
                    LargestGapMinutes = deviceGaps.Count > 0 ? deviceGaps.Max(g => g.DurationMinutes) : 0,
                    CoveragePercentage = deviceCoverage,
                    Status = deviceCoverage < 50m ? "Critical" : deviceCoverage < 80m ? "Anomalies" : "Healthy"
                };
            }).ToList();

            var suspiciousDevices = gaps
                .GroupBy(g => g.DeviceName)
                .Where(g => g.Sum(x => x.DurationMinutes) > 60)
                .Select(g => g.Key)
                .ToList();

            // Worst-status-among-devices rollup for the coarse triage string only - not an
            // average, so one healthy camera can never hide another's critical status.
            var overallStatus = deviceSummaries.Any(d => d.Status == "Critical") ? "Critical"
                : deviceSummaries.Any(d => d.Status == "Anomalies") ? "Anomalies"
                : "Healthy";

            var summary = new TimelineSummary
            {
                TotalCount = allEvents.Count,
                Status = overallStatus,
                ComplianceScore = null,
                DeviceSummaries = deviceSummaries,
                SuspiciousDevices = suspiciousDevices,
                PeakHours = peakHours,
                DetailQueryMethod = "FindSuspiciousCoordinatedActivityAsync"
            };

            summary.TopIssues["GapsDetected"] = gaps.Count;
            if (suspiciousDevices.Count > 0)
                summary.TopIssues["SuspiciousDevices"] = suspiciousDevices.Count;

            return summary;
        }

        public async Task<PaginatedResult<TimelineGap>> GetRecordingGapsPaginatedAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, int pageNumber, int pageSize, CancellationToken ct)
        {
            var allGaps = await GetRecordingGapsAsync(deviceId, fromUtc, toUtc, minGapMinutes, ct);
            var orderedGaps = allGaps.OrderBy(g => g.StartUtc).ToList();

            var totalCount = orderedGaps.Count;
            var paginatedGaps = orderedGaps
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<TimelineGap>
            {
                Items = paginatedGaps,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<CursorPaginatedResult<TimelineGap>> GetRecordingGapsCursorAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, string? cursor, int pageSize, CancellationToken ct)
        {
            var allGaps = await GetRecordingGapsAsync(deviceId, fromUtc, toUtc, minGapMinutes, ct);
            var orderedGaps = allGaps.OrderBy(g => g.StartUtc).ToList();

            int startIndex = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var cursorIndex))
            {
                startIndex = cursorIndex;
            }

            var items = orderedGaps
                .Skip(startIndex)
                .Take(pageSize)
                .ToList();

            var nextCursor = (startIndex + items.Count < orderedGaps.Count)
                ? (startIndex + items.Count).ToString()
                : null;

            return new CursorPaginatedResult<TimelineGap>
            {
                Items = items,
                NextCursor = nextCursor,
                HasMore = nextCursor != null
            };
        }
    }
}
