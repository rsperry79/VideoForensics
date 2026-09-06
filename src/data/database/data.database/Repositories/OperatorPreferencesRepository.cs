using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    public class OperatorPreferencesRepository : IOperatorPreferencesRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _contextFactory;

        public OperatorPreferencesRepository(IDbContextFactory<VideoForensicsDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<OperatorPreferences?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            return await db.OperatorPreferences.FirstOrDefaultAsync(p => p.OperatorId == operatorId, ct);
        }

        public async Task UpsertAsync(OperatorPreferences preferences, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            OperatorPreferences? existing = await db.OperatorPreferences.FirstOrDefaultAsync(p => p.OperatorId == preferences.OperatorId, ct);

            if (existing != null)
            {
                existing.ThemeMode = preferences.ThemeMode;
                existing.CultureName = preferences.CultureName;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.OperatorPreferences.Update(existing);
            }
            else
            {
                preferences.Id = preferences.Id == Guid.Empty ? Guid.NewGuid() : preferences.Id;
                preferences.UpdatedAtUtc = DateTime.UtcNow;
                _ = await db.OperatorPreferences.AddAsync(preferences, ct);
            }

            _ = await db.SaveChangesAsync(ct);
        }
    }
}
