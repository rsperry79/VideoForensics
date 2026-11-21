using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    public class AppSettingRepository : IAppSettingRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        public AppSettingRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<string?> GetAsync(string key, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                AppSetting? setting = await context.Db().AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
                return setting?.Value;
            }, ct);
        }

        public async Task SetAsync(string key, string value, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                AppSetting? existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

                // Only write when the setting doesn't exist yet or its value actually changed - avoids
                // needless UpdatedAtUtc churn and WAL growth from callers that re-save unchanged config
                // on every startup/save (e.g. ConfigurationLoader).
                if (existing != null)
                {
                    if (existing.Value == value)
                    {
                        return null;
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
                return null;
            }, ct);
        }

        public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<AppSetting>>(async context =>
            {
                return (await context.Db().AppSettings.ToListAsync(ct)).AsReadOnly();
            }, ct);
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
                if (setting != null)
                {
                    _ = db.AppSettings.Remove(setting);
                    _ = await db.SaveChangesAsync(ct);
                }

                return null;
            }, ct);
        }

        public async Task ClearAllAsync(CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                db.AppSettings.RemoveRange(await db.AppSettings.ToListAsync(ct));
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }
    }
}
