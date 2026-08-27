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
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            return setting?.Value;
        }

        public async Task SetAsync(string key, string value, CancellationToken ct)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                db.AppSettings.Update(existing);
            }
            else
            {
                await db.AppSettings.AddAsync(new AppSetting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = DateTime.UtcNow
                }, ct);
            }
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            return (await db.AppSettings.ToListAsync(ct)).AsReadOnly();
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (setting != null)
            {
                db.AppSettings.Remove(setting);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task ClearAllAsync(CancellationToken ct)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            db.AppSettings.RemoveRange(await db.AppSettings.ToListAsync(ct));
            await db.SaveChangesAsync(ct);
        }
    }
}
