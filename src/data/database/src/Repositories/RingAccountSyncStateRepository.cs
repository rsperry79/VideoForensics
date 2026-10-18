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

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public RingAccountSyncStateRepository(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            var row = await db.RingAccounts
                .AsNoTracking()
                .Where(a => a.ProviderAccountId == providerAccountId)
                .Select(a => new { a.ConfigSyncIntervalHours, a.LastConfigSyncUtc })
                .FirstOrDefaultAsync(ct);

            if (row is null)
            {
                return new RingConfigSyncState(DefaultIntervalHours, null);
            }

            return new RingConfigSyncState(Math.Max(1, row.ConfigSyncIntervalHours), row.LastConfigSyncUtc);
        }

        public async Task MarkConfigSyncCompletedAsync(Guid providerAccountId, DateTime utcNow, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            RingAccount? account = await db.RingAccounts
                .FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct);
            if (account is null)
            {
                return;
            }

            account.LastConfigSyncUtc = utcNow;
            _ = await db.SaveChangesAsync(ct);
        }

        public async Task<bool> SetConfigIntervalHoursAsync(Guid providerAccountId, int hours, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            RingAccount? account = await db.RingAccounts
                .FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct);
            if (account is null)
            {
                return false;
            }

            account.ConfigSyncIntervalHours = hours;
            _ = await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
