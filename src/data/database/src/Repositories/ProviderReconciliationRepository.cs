using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for append-only ProviderReconciliationRecord entities.</summary>
    public class ProviderReconciliationRepository : IProviderReconciliationRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProviderReconciliationRepository> _logger;

        /// <summary>Initializes a new instance of the ProviderReconciliationRepository.</summary>
        public ProviderReconciliationRepository(
            IUnitOfWork unitOfWork,
            ILogger<ProviderReconciliationRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a provider reconciliation record by ID.</summary>
        public Task<ProviderReconciliationRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(
                context => context.Db().ProviderReconciliationRecords.FirstOrDefaultAsync(prr => prr.Id == recordId, ct), ct);
        }

        /// <summary>Appends a new provider reconciliation record.</summary>
        public async Task<ProviderReconciliationRecord> AppendAsync(ProviderReconciliationRecord record, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.ProviderReconciliationRecords.Add(record);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Provider reconciliation record appended: {RecordId} (device: {DeviceId})",
                    record.Id, record.DeviceId);
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error appending provider reconciliation record for device {DeviceId}: {Message} ({ErrorType})",
                    record.DeviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets the history of reconciliation records for a device.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderReconciliationRecords
                .Where(prr => prr.DeviceId == deviceId)
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Gets open (unreviewed) discrepancies across all devices.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> GetOpenDiscrepanciesAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderReconciliationRecords
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists all provider reconciliation records.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderReconciliationRecords.ToListAsync(ct), ct);
        }
    }
}
