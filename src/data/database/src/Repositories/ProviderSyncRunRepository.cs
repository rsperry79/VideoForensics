using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderSyncRun rows.</summary>
    public class ProviderSyncRunRepository : IProviderSyncRunRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        /// <summary>Initializes a new instance of the ProviderSyncRunRepository.</summary>
        public ProviderSyncRunRepository(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Records a completed sync attempt.</summary>
        public async Task AddAsync(ProviderSyncRun run, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.ProviderSyncRuns.Add(run);
            _ = await db.SaveChangesAsync(ct);
        }

        /// <summary>Lists every sync run for an account, newest StartedUtc first.</summary>
        public async Task<IReadOnlyList<ProviderSyncRun>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderSyncRuns
                .AsNoTracking()
                .Where(r => r.ProviderAccountId == providerAccountId)
                .OrderByDescending(r => r.StartedUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets a single sync run by ID, or null if none exists.</summary>
        public async Task<ProviderSyncRun?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderSyncRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, ct);
        }
    }
}
