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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExportAuditRecordRepository> _logger;

        public ExportAuditRecordRepository(
            IUnitOfWork unitOfWork,
            ILogger<ExportAuditRecordRepository> logger)
        {
            _unitOfWork = unitOfWork;
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

                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.ExportAuditRecords.Add(record);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);

                _logger.LogDebug(
                    "Recorded export audit: LocationId={LocationId}, EventsExported={EventsExported}, Format={ExportFormat}",
                    locationId, eventsExported, exportFormat);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording export audit for location {LocationId}: {Message} ({ErrorType})", locationId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<ExportAuditRecordEntity?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportAuditRecords.FirstOrDefaultAsync(r => r.Id == recordId, ct), ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetForLocationAsync(Guid locationId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportAuditRecords
                .Where(r => r.LocationId == locationId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportAuditRecords
                .Where(r => r.ExportedBy == userId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportAuditRecords
                .Where(r => r.ExportedAtUtc >= fromUtc && r.ExportedAtUtc <= toUtc)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<ExportAuditRecordEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportAuditRecords
                .OrderByDescending(r => r.ExportedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct), ct);
        }

        public async Task<ExportStatistics> GetStatisticsAsync(CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ReadAsync<ExportStatistics>(async context =>
                {
                    List<ExportAuditRecordEntity> records = await context.Db().ExportAuditRecords.ToListAsync(ct);

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
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving export statistics: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
