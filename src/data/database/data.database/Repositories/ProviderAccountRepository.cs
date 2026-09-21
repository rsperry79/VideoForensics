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
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
        }

        /// <summary>Gets all provider accounts for a user.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.Where(pa => pa.UserId == userId).ToListAsync(ct);
        }

        /// <summary>Gets a provider account by user ID and provider name.</summary>
        public async Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.FirstOrDefaultAsync(
                pa => pa.UserId == userId && pa.ProviderName == providerName, ct);
        }

        /// <summary>Lists all provider accounts.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.ToListAsync(ct);
        }

        /// <summary>Lists active provider accounts.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.Where(pa => pa.IsActive).ToListAsync(ct);
        }

        /// <summary>Adds a new provider account.</summary>
        public async Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.ProviderAccounts.Add(account);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Provider account added: {ProviderAccountId} ({ProviderName})", account.Id, SanitizeForLog(account.ProviderName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding provider account: {ProviderName}", SanitizeForLog(account.ProviderName));
                throw;
            }
        }

        /// <summary>Updates an existing provider account.</summary>
        public async Task UpdateAsync(ProviderAccount account, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.ProviderAccounts.Update(account);
                _ = await db.SaveChangesAsync(ct);
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
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                if (account != null)
                {
                    _ = db.ProviderAccounts.Remove(account);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Provider account deleted: {ProviderAccountId}", accountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider account: {ProviderAccountId}", accountId);
                throw;
            }
        }

        /// <summary>Records an error condition for a provider account.</summary>
        public async Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(cancellationToken);
            try
            {
                ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorUtc = DateTime.UtcNow;
                    account.LastErrorMessage = errorMessage;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Provider account error recorded: {ProviderAccountId}, Message: {ErrorMessage}", providerAccountId, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording error for provider account: {ProviderAccountId}", providerAccountId);
                throw;
            }
        }

        /// <summary>Records a successful authentication for a provider account, clearing any prior error state.</summary>
        public async Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(cancellationToken);
            try
            {
                ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                if (account != null)
                {
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorUtc = null;
                    account.LastErrorMessage = null;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Provider account success recorded, error state cleared: {ProviderAccountId}", providerAccountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording success for provider account: {ProviderAccountId}", providerAccountId);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');
    }
}
