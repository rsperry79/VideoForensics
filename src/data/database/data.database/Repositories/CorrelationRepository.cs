using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    public class CorrelationRepository : ICorrelationRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<CorrelationRepository> _logger;
        private readonly ITimelineRepository _timelineRepository;
        private readonly IIntegrityRepository _integrityRepository;

        public CorrelationRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<CorrelationRepository> logger,
            ITimelineRepository timelineRepository,
            IIntegrityRepository integrityRepository)
        {
            _factory = factory;
            _logger = logger;
            _timelineRepository = timelineRepository;
            _integrityRepository = integrityRepository;
        }

        public async Task<IReadOnlyList<EventWithHealthCorrelation>> GetEventHealthCorrelationAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var correlations = await db.Events
                .Where(e => e.DeviceId == deviceId &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .Join(db.DeviceHealthRecords.Where(h => h.DeviceId == deviceId),
                      e => e.DeviceId,
                      h => h.DeviceId,
                      (e, h) => new EventWithHealthCorrelation
                      {
                          EventId = e.Id,
                          OccurredAtUtc = e.OccurredAtUtc,
                          EventType = e.EventType,
                          BatteryPercentage = h.BatteryPercentage,
                          WifiSignalRssi = h.WifiSignalRssi,
                          IsOnline = h.IsOnline,
                          HealthStatus = DetermineHealthStatus(h.BatteryPercentage, h.WifiSignalRssi, h.IsOnline)
                      })
                .ToListAsync(ct);

            return correlations;
        }

        public async Task<IReadOnlyList<HealthRelatedGap>> IdentifyHealthRelatedGapsAsync(
            Guid locationId, CancellationToken ct)
        {
            var gaps = await _timelineRepository.GetLocationRecordingGapsAsync(
                locationId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, minGapMinutes: 5, ct);

            if (gaps.Count == 0) return new List<HealthRelatedGap>();

            await using var db = await _factory.CreateDbContextAsync(ct);
            var gapDeviceIds = gaps.Select(g => g.DeviceId).Distinct().ToList();
            var healthRecords = await db.DeviceHealthRecords
                .Where(h => gapDeviceIds.Contains(h.DeviceId))
                .OrderByDescending(h => h.LastHeartbeatUtc)
                .ToListAsync(ct);

            var healthGaps = new List<HealthRelatedGap>();
            foreach (var gap in gaps)
            {
                var health = healthRecords
                    .Where(h => h.DeviceId == gap.DeviceId &&
                                h.LastHeartbeatUtc >= gap.StartUtc &&
                                h.LastHeartbeatUtc <= gap.EndUtc)
                    .FirstOrDefault();

                if (health != null)
                {
                    var issue = "Unknown";
                    if (health.BatteryPercentage < 10m) issue = "LowBattery";
                    else if (health.IsOnline == false) issue = "OfflineStatus";
                    else if (health.WifiSignalRssi < -80) issue = "PoorWiFi";

                    healthGaps.Add(new HealthRelatedGap
                    {
                        DeviceId = gap.DeviceId,
                        DeviceName = gap.DeviceName,
                        GapStartUtc = gap.StartUtc,
                        DurationMinutes = gap.DurationMinutes,
                        HealthIssue = issue,
                        MinBattery = health.BatteryPercentage,
                        MinRssi = health.WifiSignalRssi
                    });
                }
            }

            return healthGaps;
        }

        public async Task<DeviceReliabilityAnalysis> AnalyzeDeviceReliabilityAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
            var gaps = await _timelineRepository.GetRecordingGapsAsync(
                deviceId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, minGapMinutes: 5, ct);
            var healthGaps = await IdentifyHealthRelatedGapsAsync(device?.LocationId ?? Guid.Empty, ct);

            var totalMinutes = 30 * 24 * 60;
            var gapMinutes = gaps.Sum(g => g.DurationMinutes);
            var uptime = ((totalMinutes - gapMinutes) / (decimal)totalMinutes) * 100;

            return new DeviceReliabilityAnalysis
            {
                DeviceId = deviceId,
                DeviceName = device?.Name ?? "Unknown",
                UptimePercentage = uptime,
                EventCaptureRate = 95m,
                TotalGaps = gaps.Count,
                HealthRelatedGaps = healthGaps.Count(hg => hg.DeviceId == deviceId),
                ReliabilityRating = uptime > 95m ? "Excellent" : uptime > 80m ? "Good" : uptime > 50m ? "Fair" : "Poor"
            };
        }

        public async Task<IReadOnlyList<LocationChangeImpact>> GetLocationChangeHistoryAsync(
            Guid deviceId, CancellationToken ct)
        {
            // Placeholder: Would track location changes from device metadata
            // For now, return empty list (would need LocationChangeHistory entity)
            return await Task.FromResult(new List<LocationChangeImpact>());
        }

        public async Task<IReadOnlyList<LocationChangeWithGap>> CorrelateLocationChangeWithGapsAsync(
            Guid deviceId, CancellationToken ct)
        {
            var changes = await GetLocationChangeHistoryAsync(deviceId, ct);
            var gaps = await _timelineRepository.GetRecordingGapsAsync(
                deviceId, DateTime.UtcNow.AddDays(-90), DateTime.UtcNow, minGapMinutes: 5, ct);

            var correlations = new List<LocationChangeWithGap>();
            foreach (var change in changes)
            {
                var nearbyGap = gaps.FirstOrDefault(g => g.StartUtc > change.ChangedAtUtc &&
                                                          (g.StartUtc - change.ChangedAtUtc).TotalDays <= 7);
                if (nearbyGap != null)
                {
                    correlations.Add(new LocationChangeWithGap
                    {
                        DeviceId = deviceId,
                        LocationChangeUtc = change.ChangedAtUtc,
                        GapStartUtc = nearbyGap.StartUtc,
                        DaysAfterMove = (int)(nearbyGap.StartUtc - change.ChangedAtUtc).TotalDays,
                        GapDurationMinutes = nearbyGap.DurationMinutes,
                        LikelyCorrelated = (nearbyGap.StartUtc - change.ChangedAtUtc).TotalDays < 7
                    });
                }
            }

            return correlations;
        }

        public async Task<IReadOnlyList<SyncGapCorrelation>> CorrelateEventMissingWithSyncGapsAsync(
            Guid locationId, CancellationToken ct)
        {
            var missing = await _integrityRepository.GetMissingDownloadsAsync(
                locationId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, ct);

            return missing.GroupBy(m => m.DeviceId)
                .Select(g => new SyncGapCorrelation
                {
                    DeviceId = g.Key,
                    DeviceName = g.First().DeviceName,
                    SyncGapStartUtc = g.Min(x => x.OccurredAtUtc),
                    SyncGapMinutes = (int)(g.Max(x => x.OccurredAtUtc) - g.Min(x => x.OccurredAtUtc)).TotalMinutes,
                    MissingEventsDuring = g.Count(),
                    CauseLikely = true
                })
                .ToList();
        }

        public async Task<SyncHealthReport> AnalyzeSyncHealthAsync(Guid locationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
            var deviceStatus = new List<DeviceSyncStatus>();

            foreach (var device in devices)
            {
                var reliability = await AnalyzeDeviceReliabilityAsync(device.Id, ct);
                deviceStatus.Add(new DeviceSyncStatus { DeviceId = device.Id, DeviceName = device.Name, Uptime = reliability.UptimePercentage });
            }

            return new SyncHealthReport
            {
                LocationId = locationId,
                DeviceCount = devices.Count,
                TotalSyncGaps = deviceStatus.Sum(d => d.Uptime < 95m ? 1 : 0),
                LastSuccessfulSyncUtc = DateTime.UtcNow,
                DeviceStatus = deviceStatus
            };
        }

        private static string DetermineHealthStatus(decimal? battery, int? rssi, bool? isOnline)
        {
            if (isOnline == false) return "Critical";
            if (battery < 10m || rssi < -80) return "Degraded";
            return "Good";
        }

        public async Task<CorrelationSummary> GetCorrelationSummaryAsync(Guid locationId, CancellationToken ct)
        {
            var syncHealth = await AnalyzeSyncHealthAsync(locationId, ct);
            var healthGaps = await IdentifyHealthRelatedGapsAsync(locationId, ct);
            var locationChanges = new List<LocationChangeImpact>();

            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
            foreach (var device in devices)
            {
                var changes = await GetLocationChangeHistoryAsync(device.Id, ct);
                locationChanges.AddRange(changes);
            }

            var unhealthyDevices = devices
                .Where(d => syncHealth.DeviceStatus.Any(s => s.DeviceId == d.Id && s.Uptime < 95m))
                .Select(d => d.Name)
                .ToList();

            var offlineDevices = devices
                .Where(d => syncHealth.DeviceStatus.Any(s => s.DeviceId == d.Id && s.Uptime < 80m))
                .Select(d => d.Name)
                .ToList();

            // Worst-case-among-devices rollup for the coarse triage string only - not an average,
            // so one healthy camera can never hide another's offline/unhealthy status.
            var status = offlineDevices.Count > 0 ? "Critical" : unhealthyDevices.Count > 0 ? "Anomalies" : "Healthy";

            var summary = new CorrelationSummary
            {
                TotalCount = devices.Count,
                Status = status,
                ComplianceScore = null,
                DeviceCount = devices.Count,
                UnhealthyDeviceCount = unhealthyDevices.Count,
                SyncFailureCount = syncHealth.TotalSyncGaps,
                OfflineDevices = offlineDevices,
                LocationChanges = locationChanges.Select(lc => $"{lc.DeviceName} moved on {lc.ChangedAtUtc:yyyy-MM-dd}").ToList(),
                DetailQueryMethod = "AnalyzeSyncHealthAsync"
            };

            summary.TopIssues["UnhealthyDevices"] = unhealthyDevices.Count;
            summary.TopIssues["SyncGaps"] = syncHealth.TotalSyncGaps;
            summary.TopIssues["LocationChanges"] = locationChanges.Count;

            return summary;
        }

        public async Task<PaginatedResult<HealthRelatedGap>> GetHealthRelatedGapsPaginatedAsync(
            Guid locationId, int pageNumber, int pageSize, CancellationToken ct)
        {
            var allGaps = await IdentifyHealthRelatedGapsAsync(locationId, ct);
            var orderedGaps = allGaps.OrderByDescending(g => g.DurationMinutes).ToList();

            var totalCount = orderedGaps.Count;
            var paginatedGaps = orderedGaps
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<HealthRelatedGap>
            {
                Items = paginatedGaps,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<CursorPaginatedResult<EventWithHealthCorrelation>> GetEventHealthCorrelationCursorAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, string? cursor, int pageSize, CancellationToken ct)
        {
            var allEvents = await GetEventHealthCorrelationAsync(deviceId, fromUtc, toUtc, ct);
            var orderedEvents = allEvents.OrderBy(e => e.OccurredAtUtc).ToList();

            int startIndex = 0;
            if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var cursorIndex))
            {
                startIndex = cursorIndex;
            }

            var items = orderedEvents
                .Skip(startIndex)
                .Take(pageSize)
                .ToList();

            var nextCursor = (startIndex + items.Count < orderedEvents.Count)
                ? (startIndex + items.Count).ToString()
                : null;

            return new CursorPaginatedResult<EventWithHealthCorrelation>
            {
                Items = items,
                NextCursor = nextCursor,
                HasMore = nextCursor != null
            };
        }
    }
}
