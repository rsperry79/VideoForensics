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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<ProviderReconciliationRepository> _logger;

        /// <summary>Initializes a new instance of the ProviderReconciliationRepository.</summary>
        public ProviderReconciliationRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<ProviderReconciliationRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a provider reconciliation record by ID.</summary>
        public async Task<ProviderReconciliationRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderReconciliationRecords
                .FirstOrDefaultAsync(prr => prr.Id == recordId, ct);
        }

        /// <summary>Appends a new provider reconciliation record.</summary>
        public async Task<ProviderReconciliationRecord> AppendAsync(ProviderReconciliationRecord record, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.ProviderReconciliationRecords.Add(record);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Provider reconciliation record appended: {RecordId} (device: {DeviceId})",
                    record.Id, record.DeviceId);
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending provider reconciliation record for device {DeviceId}",
                    record.DeviceId);
                throw;
            }
        }

        /// <summary>Gets the history of reconciliation records for a device.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderReconciliationRecords
                .Where(prr => prr.DeviceId == deviceId)
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets open (unreviewed) discrepancies across all devices.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> GetOpenDiscrepanciesAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderReconciliationRecords
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists all provider reconciliation records.</summary>
        public async Task<IReadOnlyList<ProviderReconciliationRecord>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderReconciliationRecords.ToListAsync(ct);
        }
    }
}
