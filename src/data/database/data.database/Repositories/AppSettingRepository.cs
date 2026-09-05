using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    public class AppSettingRepository : IAppSettingRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _contextFactory;

        public AppSettingRepository(IDbContextFactory<VideoForensicsDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<string?> GetAsync(string key, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            return setting?.Value;
        }

        public async Task SetAsync(string key, string value, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            AppSetting? existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

            // Only write when the setting doesn't exist yet or its value actually changed - avoids
            // needless UpdatedAtUtc churn and WAL growth from callers that re-save unchanged config
            // on every startup/save (e.g. ConfigurationLoader).
            if (existing != null)
            {
                if (existing.Value == value)
                {
                    return;
                }

                existing.Value = value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.AppSettings.Update(existing);
            }
            else
            {
                _ = await db.AppSettings.AddAsync(new AppSetting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = DateTime.UtcNow
                }, ct);
            }

            _ = await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            return (await db.AppSettings.ToListAsync(ct)).AsReadOnly();
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (setting != null)
            {
                _ = db.AppSettings.Remove(setting);
                _ = await db.SaveChangesAsync(ct);
            }
        }

        public async Task ClearAllAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            db.AppSettings.RemoveRange(await db.AppSettings.ToListAsync(ct));
            _ = await db.SaveChangesAsync(ct);
        }
    }
}
