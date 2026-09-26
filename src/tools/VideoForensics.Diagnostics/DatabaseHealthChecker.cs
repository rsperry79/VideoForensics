using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Diagnostics.Contracts;

namespace VideoForensics.Diagnostics
{
    /// <summary>Reads database health checks without modifying data, mirroring queries from DbDiagnostics.</summary>
    public class DatabaseHealthChecker : IDatabaseHealthChecker
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        /// <summary>Initializes a new instance of the DatabaseHealthChecker.</summary>
        /// <param name="factory">The database context factory.</param>
        public DatabaseHealthChecker(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicateDevicesAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var result = await db.Devices
                .GroupBy(d => new { d.LocationId, d.ProviderDeviceId })
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateGroup(
                    g.Key.LocationId,
                    null,
                    g.Key.ProviderDeviceId,
                    g.Count(),
                    g.Select(d => d.Id).ToList() as IReadOnlyList<Guid>))
                .ToListAsync(ct);
            return result;
        }

        public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicateEventsAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var result = await db.Events
                .GroupBy(e => new { e.DeviceId, e.ProviderEventId })
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateGroup(
                    null,
                    g.Key.DeviceId,
                    g.Key.ProviderEventId,
                    g.Count(),
                    g.Select(e => e.Id).ToList() as IReadOnlyList<Guid>))
                .ToListAsync(ct);
            return result;
        }

        public async Task<DetectionRedundancySummary> GetDetectionRedundancySummaryAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var mediaDetectionCount = await db.MediaItemDetections.CountAsync(ct);
            var eventDetectionCount = await db.EventDetections.CountAsync(ct);

            double? avgDetectionsPerMediaItem = null;
            double? avgDetectionsPerEvent = null;

            if (mediaDetectionCount > 0)
            {
                var mediaItemCount = await db.MediaItems.CountAsync(ct);
                if (mediaItemCount > 0)
                {
                    avgDetectionsPerMediaItem = Math.Round((double)mediaDetectionCount / mediaItemCount, 2);
                }
            }

            if (eventDetectionCount > 0)
            {
                var eventCount = await db.Events.CountAsync(ct);
                if (eventCount > 0)
                {
                    avgDetectionsPerEvent = Math.Round((double)eventDetectionCount / eventCount, 2);
                }
            }

            return new DetectionRedundancySummary(
                mediaDetectionCount,
                eventDetectionCount,
                avgDetectionsPerMediaItem,
                avgDetectionsPerEvent);
        }

        public async Task<DeviceHealthSummary> GetDeviceHealthSummaryAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var deviceHealthCount = await db.DeviceHealths.CountAsync(ct);

            var healthDates = await db.DeviceHealths
                .GroupBy(h => h.CapturedAtUtc.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new DateHealthCount(g.Key, g.Count()))
                .Take(5)
                .ToListAsync(ct);

            return new DeviceHealthSummary(deviceHealthCount, healthDates);
        }

        public async Task<DeviceFeatureOverlapSummary> GetDeviceFeatureOverlapAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var deviceCapabilitiesCount = await db.DeviceCapabilities.CountAsync(ct);
            var deviceFeaturesCount = await db.DeviceFeatures.CountAsync(ct);

            var devicesWithBothIds = await db.DeviceCapabilities
                .Join(db.DeviceFeatures, dc => dc.DeviceId, df => df.DeviceId, (dc, df) => new { dc.DeviceId })
                .Select(x => x.DeviceId)
                .Distinct()
                .ToListAsync(ct);

            var devicesWithBoth = devicesWithBothIds.Count;

            var capOnlyCount = await db.DeviceCapabilities
                .Where(dc => !db.DeviceFeatures.Any(df => df.DeviceId == dc.DeviceId))
                .Select(x => x.DeviceId)
                .Distinct()
                .CountAsync(ct);

            var featuresOnlyCount = await db.DeviceFeatures
                .Where(df => !db.DeviceCapabilities.Any(dc => dc.DeviceId == df.DeviceId))
                .Select(x => x.DeviceId)
                .Distinct()
                .CountAsync(ct);

            return new DeviceFeatureOverlapSummary(
                deviceCapabilitiesCount,
                deviceFeaturesCount,
                devicesWithBoth,
                devicesWithBothIds,
                capOnlyCount,
                featuresOnlyCount);
        }

        public async Task<OrphanedRecordSummary> FindOrphanedRecordsAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var orphanedEventIds = await db.Events
                .Where(e => !db.Devices.Any(d => d.Id == e.DeviceId))
                .Select(e => e.Id)
                .ToListAsync(ct);

            var orphanedMediaItemIds = await db.MediaItems
                .Where(m => !db.Devices.Any(d => d.Id == m.DeviceId))
                .Select(m => m.Id)
                .ToListAsync(ct);

            var orphanedMediaItemDetectionIds = await db.MediaItemDetections
                .Where(mid => !db.MediaItems.Any(m => m.Id == mid.MediaItemId))
                .Select(mid => mid.Id)
                .ToListAsync(ct);

            var orphanedEventDetectionIds = await db.EventDetections
                .Where(ed => !db.Events.Any(e => e.Id == ed.EventId))
                .Select(ed => ed.Id)
                .ToListAsync(ct);

            var orphanedDeviceIds = await db.Devices
                .Where(d => !db.Locations.Any(l => l.Id == d.LocationId))
                .Select(d => d.Id)
                .ToListAsync(ct);

            var orphanedDownloadEventIds = await db.DownloadEvents
                .Where(de => !db.Devices.Any(d => d.Id == de.DeviceId))
                .Select(de => de.Id)
                .ToListAsync(ct);

            return new OrphanedRecordSummary(
                orphanedEventIds.Count,
                orphanedEventIds,
                orphanedMediaItemIds.Count,
                orphanedMediaItemIds,
                orphanedMediaItemDetectionIds.Count,
                orphanedMediaItemDetectionIds,
                orphanedEventDetectionIds.Count,
                orphanedEventDetectionIds,
                orphanedDeviceIds.Count,
                orphanedDeviceIds,
                orphanedDownloadEventIds.Count,
                orphanedDownloadEventIds);
        }

        public async Task<IReadOnlyList<TableSizeEntry>> GetTableSizesAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var sizes = new List<TableSizeEntry>
            {
                new("MediaItems", await db.MediaItems.CountAsync(ct)),
                new("Events", await db.Events.CountAsync(ct)),
                new("Devices", await db.Devices.CountAsync(ct)),
                new("Locations", await db.Locations.CountAsync(ct)),
                new("MediaItemDetections", await db.MediaItemDetections.CountAsync(ct)),
                new("EventDetections", await db.EventDetections.CountAsync(ct)),
                new("DeviceHealth", await db.DeviceHealths.CountAsync(ct)),
                new("DeviceCapabilities", await db.DeviceCapabilities.CountAsync(ct)),
                new("DeviceFeatures", await db.DeviceFeatures.CountAsync(ct)),
                new("DownloadEvents", await db.DownloadEvents.CountAsync(ct))
            };

            return sizes;
        }
    }
}
