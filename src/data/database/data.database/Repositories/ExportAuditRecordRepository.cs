using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for export audit trail tracking.</summary>
    public class ExportAuditRecordRepository : IExportAuditRecordRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<ExportAuditRecordRepository> _logger;

        public ExportAuditRecordRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<ExportAuditRecordRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<ExportAuditRecordEntity> RecordExportAsync(
            Guid locationId,
            string exportedBy,
            int eventsExported,
            string exportFormat,
            string purpose,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var record = new ExportAuditRecordEntity
                {
                    Id = Guid.NewGuid(),
                    LocationId = locationId,
                    ExportedBy = exportedBy,
                    EventsExported = eventsExported,
                    ExportFormat = exportFormat,
                    Purpose = purpose,
                    ExportedAtUtc = DateTime.UtcNow
                };

                _ = db.ExportAuditRecords.Add(record);
                _ = await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Recorded export audit: LocationId={LocationId}, ExportedBy={ExportedBy}, EventsExported={EventsExported}, Format={ExportFormat}",
                    locationId, exportedBy, eventsExported, exportFormat);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording export audit for location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<ExportAuditRecordEntity?> GetAsync(Guid recordId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportAuditRecords.FirstOrDefaultAsync(r => r.Id == recordId, ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetForLocationAsync(Guid locationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportAuditRecords
                .Where(r => r.LocationId == locationId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportAuditRecords
                .Where(r => r.ExportedBy == userId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportAuditRecords
                .Where(r => r.ExportedAtUtc >= fromUtc && r.ExportedAtUtc <= toUtc)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportAuditRecords
                .OrderByDescending(r => r.ExportedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<ExportStatistics> GetStatisticsAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var records = await db.ExportAuditRecords.ToListAsync(ct);

                var statistics = new ExportStatistics
                {
                    TotalExports = records.Count,
                    TotalEventsExported = records.Sum(r => r.EventsExported),
                    FirstExportAtUtc = records.Count > 0 ? records.Min(r => r.ExportedAtUtc) : null,
                    LastExportAtUtc = records.Count > 0 ? records.Max(r => r.ExportedAtUtc) : null,
                    UniqueExporters = records.Select(r => r.ExportedBy).Distinct().Count(),
                    ExportsByFormat = records
                        .GroupBy(r => r.ExportFormat)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                _logger.LogDebug(
                    "Export statistics: Total={Total}, Events={Events}, Formats={Formats}, Exporters={Exporters}",
                    statistics.TotalExports,
                    statistics.TotalEventsExported,
                    statistics.ExportsByFormat.Count,
                    statistics.UniqueExporters);

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving export statistics");
                throw;
            }
        }
    }
}
