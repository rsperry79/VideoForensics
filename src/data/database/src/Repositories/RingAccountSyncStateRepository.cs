using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Reads and writes the config-sync gate columns on RingAccounts. The interval is clamped to at least 1 on read,
    /// because the schema carries no CHECK constraint (the migration is AddColumn-only).
    /// </summary>
    public class RingAccountSyncStateRepository : IRingAccountSyncState
    {
        /// <summary>Interval used when the account row does not exist yet.</summary>
        private const int DefaultIntervalHours = 24;

        private readonly IUnitOfWork _unitOfWork;

        public RingAccountSyncStateRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct)
        {
            var row = await _unitOfWork.ReadAsync(context => context.Db().RingAccounts
                .AsNoTracking()
                .Where(a => a.ProviderAccountId == providerAccountId)
                .Select(a => new { a.ConfigSyncIntervalHours, a.LastConfigSyncUtc })
                .FirstOrDefaultAsync(ct), ct);

            return row is null
                ? new RingConfigSyncState(DefaultIntervalHours, null)
                : new RingConfigSyncState(Math.Max(1, row.ConfigSyncIntervalHours), row.LastConfigSyncUtc);
        }

        public async Task MarkConfigSyncCompletedAsync(Guid providerAccountId, DateTime utcNow, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                RingAccount? account = await db.RingAccounts
                    .FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct);
                if (account is null)
                {
                    return null;
                }

                account.LastConfigSyncUtc = utcNow;
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        public Task<bool> SetConfigIntervalHoursAsync(Guid providerAccountId, int hours, CancellationToken ct)
        {
            return _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                RingAccount? account = await db.RingAccounts
                    .FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct);
                if (account is null)
                {
                    return false;
                }

                account.ConfigSyncIntervalHours = hours;
                _ = await db.SaveChangesAsync(ct);
                return true;
            }, ct);
        }
    }
}
