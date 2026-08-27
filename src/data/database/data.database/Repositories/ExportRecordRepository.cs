using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ExportRecord and ExportRecordItem entities.</summary>
    public class ExportRecordRepository : IExportRecordRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<ExportRecordRepository> _logger;

        /// <summary>Initializes a new instance of the ExportRecordRepository.</summary>
        public ExportRecordRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<ExportRecordRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets an export record by ID.</summary>
        public async Task<ExportRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportRecords.FirstOrDefaultAsync(er => er.Id == recordId, ct);
        }

        /// <summary>Gets export record items for an export record.</summary>
        public async Task<IReadOnlyList<ExportRecordItem>> GetItemsForRecordAsync(Guid exportRecordId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportRecordItems
                .Where(eri => eri.ExportRecordId == exportRecordId)
                .ToListAsync(ct);
        }

        /// <summary>Appends a new export record with its items in a single atomic operation.</summary>
        public async Task<ExportRecord> AppendAsync(
            ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.ExportRecords.Add(record);
                db.ExportRecordItems.AddRange(items);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Export record appended: {ExportRecordId} (items: {ItemCount})",
                    record.Id, items.Count);
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending export record with {ItemCount} items", items.Count);
                throw;
            }
        }

        /// <summary>Gets export history for a specific media item.</summary>
        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportRecordItems
                .Where(eri => eri.MediaItemId == mediaItemId)
                .Select(eri => eri.ExportRecordId)
                .Distinct()
                .Join(
                    db.ExportRecords,
                    recordId => recordId,
                    record => record.Id,
                    (_, record) => record)
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets export history for a specific device.</summary>
        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportRecords
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists all export records.</summary>
        public async Task<IReadOnlyList<ExportRecord>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ExportRecords.ToListAsync(ct);
        }
    }
}
