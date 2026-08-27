using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for ProviderAccount entities.</summary>
    public class ProviderAccountRepository : IProviderAccountRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<ProviderAccountRepository> _logger;

        /// <summary>Initializes a new instance of the ProviderAccountRepository.</summary>
        public ProviderAccountRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<ProviderAccountRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a provider account by ID.</summary>
        public async Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
        }

        /// <summary>Gets all provider accounts for a user.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.Where(pa => pa.UserId == userId).ToListAsync(ct);
        }

        /// <summary>Gets a provider account by user ID and provider name.</summary>
        public async Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.FirstOrDefaultAsync(
                pa => pa.UserId == userId && pa.ProviderName == providerName, ct);
        }

        /// <summary>Lists all provider accounts.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.ToListAsync(ct);
        }

        /// <summary>Lists active provider accounts.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.Where(pa => pa.IsActive).ToListAsync(ct);
        }

        /// <summary>Adds a new provider account.</summary>
        public async Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.ProviderAccounts.Add(account);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Provider account added: {ProviderAccountId} ({ProviderName})", account.Id, account.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding provider account: {ProviderName}", account.ProviderName);
                throw;
            }
        }

        /// <summary>Updates an existing provider account.</summary>
        public async Task UpdateAsync(ProviderAccount account, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.ProviderAccounts.Update(account);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Provider account updated: {ProviderAccountId}", account.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider account: {ProviderAccountId}", account.Id);
                throw;
            }
        }

        /// <summary>Deletes a provider account.</summary>
        public async Task DeleteAsync(Guid accountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                if (account != null)
                {
                    db.ProviderAccounts.Remove(account);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Provider account deleted: {ProviderAccountId}", accountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider account: {ProviderAccountId}", accountId);
                throw;
            }
        }
    }
}
