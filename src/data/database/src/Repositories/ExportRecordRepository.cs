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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExportRecordRepository> _logger;

        /// <summary>Initializes a new instance of the ExportRecordRepository.</summary>
        public ExportRecordRepository(IUnitOfWork unitOfWork, ILogger<ExportRecordRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets an export record by ID.</summary>
        public async Task<ExportRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportRecords.FirstOrDefaultAsync(er => er.Id == recordId, ct), ct);
        }

        /// <summary>Gets export record items for an export record.</summary>
        public async Task<IReadOnlyList<ExportRecordItem>> GetItemsForRecordAsync(Guid exportRecordId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportRecordItems
                .Where(eri => eri.ExportRecordId == exportRecordId)
                .ToListAsync(ct), ct);
        }

        /// <summary>Appends a new export record with its items in a single atomic operation.</summary>
        public async Task<ExportRecord> AppendAsync(
            ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.ExportRecords.Add(record);
                    db.ExportRecordItems.AddRange(items);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Export record appended: {ExportRecordId} (items: {ItemCount})",
                    record.Id, items.Count);
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error appending export record with {ItemCount} items: {Message} ({ErrorType})", items.Count, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets export history for a specific media item.</summary>
        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<ExportRecord>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
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
            }, ct);
        }

        /// <summary>Gets export history for a specific device.</summary>
        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportRecords
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists all export records.</summary>
        public async Task<IReadOnlyList<ExportRecord>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ExportRecords.ToListAsync(ct), ct);
        }
    }
}
