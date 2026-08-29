using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository for evidence integrity verification and audit trails.</summary>
    public class IntegrityRepository : IIntegrityRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<IntegrityRepository> _logger;

        public IntegrityRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<IntegrityRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<DownloadAuditRecord>> GetDownloadHistoryAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
            var events = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);

            return events.Select(e => new DownloadAuditRecord
            {
                EventId = e.Id,
                DeviceId = deviceId,
                DeviceName = device?.Name ?? "Unknown",
                OccurredAtUtc = e.OccurredAtUtc,
                DownloadedAtUtc = e.DownloadedAtUtc,
                DelayMinutes = e.DownloadedAtUtc.HasValue ? (int)(e.DownloadedAtUtc.Value - e.OccurredAtUtc).TotalMinutes : 0,
                EventType = e.EventType,
                DownloadStatus = e.DownloadedAtUtc.HasValue ? "Downloaded" : "Missing"
            }).ToList();
        }

        public async Task<IReadOnlyList<DownloadAuditRecord>> GetLocationDownloadHistoryAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events
                .Join(db.Devices.Where(d => d.LocationId == locationId),
                      e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Event.OccurredAtUtc >= fromUtc && x.Event.OccurredAtUtc <= toUtc)
                .OrderBy(x => x.Event.OccurredAtUtc)
                .Select(x => new DownloadAuditRecord
                {
                    EventId = x.Event.Id,
                    DeviceId = x.Device.Id,
                    DeviceName = x.Device.Name,
                    OccurredAtUtc = x.Event.OccurredAtUtc,
                    DownloadedAtUtc = x.Event.DownloadedAtUtc,
                    DelayMinutes = x.Event.DownloadedAtUtc.HasValue ? (int)(x.Event.DownloadedAtUtc.Value - x.Event.OccurredAtUtc).TotalMinutes : 0,
                    EventType = x.Event.EventType,
                    DownloadStatus = x.Event.DownloadedAtUtc.HasValue ? "Downloaded" : "Missing"
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<MissingDownloadRecord>> GetMissingDownloadsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var missing = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc &&
                            x.Event.DownloadedAtUtc == null)
                .Select(x => new MissingDownloadRecord
                {
                    EventId = x.Event.Id,
                    DeviceId = x.Device.Id,
                    DeviceName = x.Device.Name,
                    OccurredAtUtc = x.Event.OccurredAtUtc,
                    DiscoveredAtUtc = x.Event.DiscoveredAtUtc,
                    EventType = x.Event.EventType,
                    Reason = "NotRequested"
                })
                .ToListAsync(ct);

            return missing;
        }

        public async Task<DownloadCompletenessReport> VerifyDownloadCompletenessAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var allEvents = await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);

            var downloaded = allEvents.Count(x => x.Event.DownloadedAtUtc.HasValue);
            var missing = await GetMissingDownloadsAsync(locationId, fromUtc, toUtc, ct);

            var completeness = allEvents.Count > 0 ? (decimal)downloaded / allEvents.Count * 100 : 100m;

            return new DownloadCompletenessReport
            {
                LocationId = locationId,
                AnalysisFromUtc = fromUtc,
                AnalysisToUtc = toUtc,
                TotalEvents = allEvents.Count,
                DownloadedEvents = downloaded,
                MissingEvents = missing.Count,
                CompletenessPercentage = completeness,
                MissingRecords = missing.ToList(),
                Status = completeness < 50m ? "Critical" : completeness < 95m ? "Incomplete" : "Complete"
            };
        }

        public async Task<IReadOnlyList<TamperingIndicator>> VerifyEventHashesAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
            var events = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc &&
                            e.ApiSourceHash != null)
                .ToListAsync(ct);

            var indicators = new List<TamperingIndicator>();
            foreach (var e in events)
            {
                if (e.ApiSourceHash != e.EventIntegrityHash && e.EventIntegrityHash != null)
                {
                    indicators.Add(new TamperingIndicator
                    {
                        EventId = e.Id,
                        DeviceId = deviceId,
                        DeviceName = device?.Name ?? "Unknown",
                        OccurredAtUtc = e.OccurredAtUtc,
                        IndicatorType = "HashMismatch",
                        Description = $"Event hash mismatch: API={e.ApiSourceHash?.Substring(0, 8)}... vs Local={e.EventIntegrityHash?.Substring(0, 8)}...",
                        TamperingScore = 85
                    });
                }
            }

            return indicators;
        }

        public async Task<IReadOnlyList<TamperingIndicator>> GetTamperingIndicatorsAsync(
            Guid locationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
            var allIndicators = new List<TamperingIndicator>();

            foreach (var device in devices)
            {
                var indicators = await VerifyEventHashesAsync(device.Id, DateTime.MinValue, DateTime.MaxValue, ct);
                allIndicators.AddRange(indicators);
            }

            return allIndicators.OrderByDescending(i => i.TamperingScore).ToList();
        }

        public async Task<int> ComputeEventIntegrityScoreAsync(Guid locationId, CancellationToken ct)
        {
            var tampering = await GetTamperingIndicatorsAsync(locationId, ct);
            var completeness = await VerifyDownloadCompletenessAsync(
                locationId, DateTime.UtcNow.AddDays(-180), DateTime.UtcNow, ct);

            var tamperingPenalty = Math.Min(50, tampering.Count * 5);
            var completenessScore = (int)completeness.CompletenessPercentage;
            var score = Math.Max(0, completenessScore - tamperingPenalty);

            _logger.LogInformation(
                "Integrity score for {LocationId}: {Score}% (Completeness={Completeness}%, Tampering Indicators={TamperingCount})",
                locationId, score, completenessScore, tampering.Count);

            return score;
        }

        public async Task<IReadOnlyList<AnomalousGap>> DetectMissingEventsByPatternAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
            var events = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);

            var baselineRate = events.Count / ((toUtc - fromUtc).TotalHours + 1);
            var gaps = new List<AnomalousGap>();

            for (int i = 0; i < events.Count - 1; i++)
            {
                var gapDuration = (events[i + 1].OccurredAtUtc - events[i].OccurredAtUtc).TotalMinutes;
                var expectedEvents = (decimal)(gapDuration / 60) * (decimal)baselineRate;

                if (gapDuration > 30 && expectedEvents > 0)
                {
                    gaps.Add(new AnomalousGap
                    {
                        DeviceId = deviceId,
                        DeviceName = device?.Name ?? "Unknown",
                        StartUtc = events[i].OccurredAtUtc,
                        EndUtc = events[i + 1].OccurredAtUtc,
                        DurationMinutes = (int)gapDuration,
                        BaselineEventRate = (decimal)baselineRate,
                        ExpectedEvents = expectedEvents,
                        ActualEvents = 0,
                        Anomaly = "Missing"
                    });
                }
            }

            return gaps;
        }

        public async Task<IReadOnlyList<RecordingFailure>> IdentifyRecordingFailuresAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var failures = new List<RecordingFailure>();
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);

            foreach (var device in devices)
            {
                var gaps = await DetectMissingEventsByPatternAsync(device.Id, fromUtc, toUtc, ct);
                foreach (var gap in gaps)
                {
                    var health = await db.DeviceHealthRecords
                        .Where(h => h.DeviceId == device.Id &&
                                    h.LastHeartbeatUtc >= gap.StartUtc &&
                                    h.LastHeartbeatUtc <= gap.EndUtc)
                        .FirstOrDefaultAsync(ct);

                    var failureType = "Unknown";
                    if (health?.IsOnline == false) failureType = "DeviceOffline";
                    else if (health?.BatteryPercentage < 10m) failureType = "LowBattery";
                    else if (health?.WifiSignalRssi < -80) failureType = "NoConnectivity";

                    failures.Add(new RecordingFailure
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        FailureAtUtc = gap.StartUtc,
                        RecoveryAtUtc = gap.EndUtc,
                        DurationMinutes = gap.DurationMinutes,
                        FailureType = failureType,
                        Evidence = health != null ? $"Battery={health.BatteryPercentage}% RSSI={health.WifiSignalRssi}" : "Unknown"
                    });
                }
            }

            return failures;
        }

        public async Task<IReadOnlyList<SuspiciousGap>> FlagSuspiciousGapsAsync(Guid locationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var suspiciousGaps = new List<SuspiciousGap>();
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);

            foreach (var device in devices)
            {
                var gaps = await DetectMissingEventsByPatternAsync(device.Id, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, ct);
                foreach (var gap in gaps)
                {
                    var suspicion = gap.StartUtc.Hour switch
                    {
                        >= 22 or < 6 => "NightGap",
                        _ => gap.StartUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "WeekendGap" : "LongGap"
                    };

                    suspiciousGaps.Add(new SuspiciousGap
                    {
                        LocationId = locationId,
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        GapStartUtc = gap.StartUtc,
                        GapEndUtc = gap.EndUtc,
                        DurationMinutes = gap.DurationMinutes,
                        Suspicion = suspicion,
                        SuspicionScore = gap.DurationMinutes > 60 ? 75 : 50
                    });
                }
            }

            return suspiciousGaps.OrderByDescending(g => g.SuspicionScore).ToList();
        }

        public async Task<IntegritySummary> GetIntegritySummaryAsync(Guid locationId, CancellationToken ct)
        {
            var integrityScore = await ComputeEventIntegrityScoreAsync(locationId, ct);
            var tampering = await GetTamperingIndicatorsAsync(locationId, ct);
            var completeness = await VerifyDownloadCompletenessAsync(
                locationId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, ct);
            var failures = await IdentifyRecordingFailuresAsync(
                locationId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, ct);

            var compromisedDevices = tampering
                .GroupBy(t => t.DeviceName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            var summary = new IntegritySummary
            {
                TotalCount = completeness.TotalEvents,
                Status = integrityScore >= 80 ? "Healthy" : integrityScore >= 50 ? "Anomalies" : "Critical",
                ComplianceScore = integrityScore,
                TamperingIndicators = tampering.Count,
                MissingDownloads = completeness.MissingEvents,
                FailedRecordings = failures.Count,
                IntegrityScore = (decimal)integrityScore,
                CompromisedDevices = compromisedDevices,
                DetailQueryMethod = "GetTamperingIndicatorsAsync"
            };

            summary.TopIssues["HashMismatches"] = tampering.Count;
            summary.TopIssues["MissingDownloads"] = completeness.MissingEvents;
            summary.TopIssues["RecordingFailures"] = failures.Count;

            return summary;
        }

        public async Task<PaginatedResult<TamperingIndicator>> GetTamperingIndicatorsPaginatedAsync(
            Guid locationId, int pageNumber, int pageSize, CancellationToken ct)
        {
            var allIndicators = await GetTamperingIndicatorsAsync(locationId, ct);
            var orderedIndicators = allIndicators.OrderByDescending(i => i.TamperingScore).ToList();

            var totalCount = orderedIndicators.Count;
            var paginatedIndicators = orderedIndicators
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<TamperingIndicator>
            {
                Items = paginatedIndicators,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<CursorPaginatedResult<DownloadAuditRecord>> GetDownloadHistoryCursorAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, string? cursor, int pageSize, CancellationToken ct)
        {
            var allRecords = await GetDownloadHistoryAsync(deviceId, fromUtc, toUtc, ct);
            var orderedRecords = allRecords.OrderBy(r => r.OccurredAtUtc).ToList();

            int startIndex = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var cursorIndex))
            {
                startIndex = cursorIndex;
            }

            var items = orderedRecords
                .Skip(startIndex)
                .Take(pageSize)
                .ToList();

            var nextCursor = (startIndex + items.Count < orderedRecords.Count)
                ? (startIndex + items.Count).ToString()
                : null;

            return new CursorPaginatedResult<DownloadAuditRecord>
            {
                Items = items,
                NextCursor = nextCursor,
                HasMore = nextCursor != null
            };
        }
    }
}
