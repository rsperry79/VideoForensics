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
                // If the row is locked and UiMode is being changed, throw
                if (existing.UiModeLocked && existing.UiMode != preferences.UiMode)
                {
                    throw new InvalidOperationException("The display mode is locked by an administrator.");
                }

                existing.ThemeMode = preferences.ThemeMode;
                existing.CultureName = preferences.CultureName;
                existing.UiMode = preferences.UiMode;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                // NOTE: UiModeLocked is NEVER changed by UpsertAsync; it is always left as-is
                _ = db.OperatorPreferences.Update(existing);
            }
            else
            {
                preferences.Id = preferences.Id == Guid.Empty ? Guid.NewGuid() : preferences.Id;
                preferences.UpdatedAtUtc = DateTime.UtcNow;
                // NOTE: New rows are always created with UiModeLocked = false, regardless of incoming value
                preferences.UiModeLocked = false;
                _ = await db.OperatorPreferences.AddAsync(preferences, ct);
            }

            _ = await db.SaveChangesAsync(ct);
        }

        public async Task<OperatorPreferences> SetUiModeAsync(Guid operatorId, string uiMode, bool locked, CancellationToken ct)
        {
            // Validate uiMode
            if (uiMode != "Standard" && uiMode != "Simple")
            {
                throw new ArgumentException($"UiMode must be either 'Standard' or 'Simple', but got '{uiMode}'.", nameof(uiMode));
            }

            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            OperatorPreferences? existing = await db.OperatorPreferences.FirstOrDefaultAsync(p => p.OperatorId == operatorId, ct);

            if (existing != null)
            {
                existing.UiMode = uiMode;
                existing.UiModeLocked = locked;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.OperatorPreferences.Update(existing);
            }
            else
            {
                existing = new OperatorPreferences
                {
                    Id = Guid.NewGuid(),
                    OperatorId = operatorId,
                    ThemeMode = "System",
                    CultureName = null,
                    UiMode = uiMode,
                    UiModeLocked = locked,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _ = await db.OperatorPreferences.AddAsync(existing, ct);
            }

            _ = await db.SaveChangesAsync(ct);
            return existing;
        }
    }
}