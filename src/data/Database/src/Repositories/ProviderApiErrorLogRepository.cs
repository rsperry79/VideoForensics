using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderApiErrorLog rows.</summary>
    public class ProviderApiErrorLogRepository : IProviderApiErrorLogRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public ProviderApiErrorLogRepository(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public async Task RecordAsync(ProviderApiErrorLog entry, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.ProviderApiErrorLogs.Add(entry);
            _ = await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<ProviderApiErrorLog>> GetByEventIdAsync(Guid eventId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderApiErrorLogs
                .Where(e => e.EventId == eventId)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ProviderApiErrorLog>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderApiErrorLogs
                .Where(e => e.DeviceId == deviceId)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);
        }
    }
}
