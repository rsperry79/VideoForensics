using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderSyncRun rows.</summary>
    public class ProviderSyncRunRepository : IProviderSyncRunRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Initializes a new instance of the ProviderSyncRunRepository.</summary>
        public ProviderSyncRunRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>Records a completed sync attempt.</summary>
        public async Task AddAsync(ProviderSyncRun run, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.ProviderSyncRuns.Add(run);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        /// <summary>Lists every sync run for an account, newest StartedUtc first.</summary>
        public async Task<IReadOnlyList<ProviderSyncRun>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderSyncRuns
                .AsNoTracking()
                .Where(r => r.ProviderAccountId == providerAccountId)
                .OrderByDescending(r => r.StartedUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Gets a single sync run by ID, or null if none exists.</summary>
        public Task<ProviderSyncRun?> GetAsync(Guid id, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().ProviderSyncRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, ct), ct);
        }
    }
}
