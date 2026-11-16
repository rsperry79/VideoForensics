using Microsoft.EntityFrameworkCore;
using VideoForensics.Data.Database.Logs;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderApiErrorLog rows.</summary>
    public class ProviderApiErrorLogRepository : IProviderApiErrorLogRepository
    {
        private readonly ILogsDbGate _logs;

        public ProviderApiErrorLogRepository(ILogsDbGate logs)
        {
            _logs = logs;
        }

        public async Task RecordAsync(ProviderApiErrorLog entry, CancellationToken ct)
        {
            await using LogsWriteLease lease = await _logs.AcquireWriteAsync(ct);
            VideoForensicsLogsDbContext db = lease.Db;
            _ = db.ProviderApiErrorLogs.Add(entry);
            _ = await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<ProviderApiErrorLog>> GetByEventIdAsync(Guid eventId, CancellationToken ct)
        {
            await using VideoForensicsLogsDbContext db = await _logs.OpenReadAsync(ct);
            return await db.ProviderApiErrorLogs
                .Where(e => e.EventId == eventId)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ProviderApiErrorLog>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsLogsDbContext db = await _logs.OpenReadAsync(ct);
            return await db.ProviderApiErrorLogs
                .Where(e => e.DeviceId == deviceId)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);
        }

        public async Task<int> PruneOlderThanAsync(TimeSpan age, CancellationToken ct)
        {
            await using LogsWriteLease lease = await _logs.AcquireWriteAsync(ct);
            VideoForensicsLogsDbContext db = lease.Db;
            DateTime cutoff = DateTime.UtcNow - age;
            return await db.ProviderApiErrorLogs
                .Where(e => e.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);
        }
    }
}
