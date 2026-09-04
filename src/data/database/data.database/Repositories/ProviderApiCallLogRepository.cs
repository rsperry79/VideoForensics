using Microsoft.EntityFrameworkCore;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderApiCallRecord rows.</summary>
    public class ProviderApiCallLogRepository : IProviderApiCallLogRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public ProviderApiCallLogRepository(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public async Task RecordCallAsync(string providerName, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.ProviderApiCallRecords.Add(new ProviderApiCallRecord
            {
                Id = Guid.NewGuid(),
                ProviderName = providerName,
                TimestampUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        public async Task<int> CountRecentCallsAsync(string providerName, TimeSpan window, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var since = DateTime.UtcNow - window;
            return await db.ProviderApiCallRecords
                .CountAsync(r => r.ProviderName == providerName && r.TimestampUtc >= since, ct);
        }

        public async Task PruneOlderThanAsync(TimeSpan retain, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var cutoff = DateTime.UtcNow - retain;
            var stale = await db.ProviderApiCallRecords.Where(r => r.TimestampUtc < cutoff).ToListAsync(ct);
            if (stale.Count > 0)
            {
                db.ProviderApiCallRecords.RemoveRange(stale);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
