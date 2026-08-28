using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Ring account entities.</summary>
    public class RingAccountRepository : IRingAccountRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<RingAccountRepository> _logger;

        public RingAccountRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<RingAccountRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<RingAccount?> GetAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.RingAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<RingAccount?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.RingAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct);
        }

        public async Task AddAsync(RingAccount account, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.RingAccounts.Add(account);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Ring account added: {AccountId} (subscription: {SubscriptionLevel})",
                    account.Id, account.SubscriptionLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding Ring account");
                throw;
            }
        }

        public async Task UpdateAsync(RingAccount account, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.RingAccounts.Update(account);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Ring account updated: {AccountId}", account.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Ring account: {AccountId}", account.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var account = await db.RingAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (account != null)
                {
                    db.RingAccounts.Remove(account);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Ring account deleted: {AccountId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Ring account: {AccountId}", id);
                throw;
            }
        }
    }
}
