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

        /// <summary>Lists active provider accounts (excludes removed accounts).</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.ProviderAccounts.Where(pa => pa.IsActive && pa.RemovedUtc == null).ToListAsync(ct);
        }

        /// <summary>Lists removed provider accounts (data retained), most recently removed first.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListRemovedAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            // SQLite cannot ORDER BY a DateTime in SQL reliably across providers, but the removed set is small; sort client-side.
            List<ProviderAccount> removed = await db.ProviderAccounts.Where(pa => pa.RemovedUtc != null).ToListAsync(ct);
            return removed.OrderByDescending(pa => pa.RemovedUtc).ToList();
        }

        /// <summary>Retires a provider account, keeping its row and data. The step-up token is used only by the remote implementation and ignored here.</summary>
        public async Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            ProviderAccount? account = null;
            try
            {
                account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                if (account == null || account.RemovedUtc != null)
                {
                    return;
                }

                account.RemovedUtc = removedUtc;
                account.IsActive = false;

                // SyncSchedule and Credentials have no cascading FK to ProviderAccount; remove them here so nothing keeps
                // scheduling or authenticating a removed account. The account row and its collected data are retained.
                SyncSchedule? schedule = await db.SyncSchedules.FirstOrDefaultAsync(s => s.ProviderAccountId == accountId, ct);
                if (schedule != null)
                {
                    _ = db.SyncSchedules.Remove(schedule);
                }

                List<Credential> credentials = await db.Credentials.Where(c => c.ProviderAccountId == accountId).ToListAsync(ct);
                db.Credentials.RemoveRange(credentials);

                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Provider account retired: {ProviderAccountId} ({ProviderName})", accountId, SanitizeForLog(account.ProviderName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retiring provider account: {ProviderAccountId} ({ProviderName})", accountId, SanitizeForLog(account?.ProviderName ?? "unknown"));
                throw;
            }
        }

        /// <summary>Adds a new provider account.</summary>
        public async Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.ProviderAccounts.Add(account);
                await AddDefaultScheduleIfMissingAsync(db, account.Id, ct);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Provider account added: {ProviderName}", SanitizeForLog(account.ProviderName));
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
                _logger.LogDebug("Provider account updated: {ProviderName}", SanitizeForLog(account.ProviderName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider account: {ProviderName}", SanitizeForLog(account.ProviderName));
                throw;
            }
        }

        /// <summary>Deletes a provider account and its sync schedule in one atomic save.</summary>
        public async Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            ProviderAccount? account = null;
            try
            {
                account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                if (account != null)
                {
                    // SyncSchedule has no FK to ProviderAccount, so nothing cascades. Remove it here or the
                    // scheduler keeps ticking an orphan and logs "Account not found" every interval.
                    SyncSchedule? schedule = await db.SyncSchedules.FirstOrDefaultAsync(s => s.ProviderAccountId == accountId, ct);
                    if (schedule != null)
                    {
                        _ = db.SyncSchedules.Remove(schedule);
                    }

                    _ = db.ProviderAccounts.Remove(account);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogDebug("Provider account deleted: {ProviderName}", SanitizeForLog(account.ProviderName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider account: {ProviderName}", SanitizeForLog(account?.ProviderName ?? "unknown"));
                throw;
            }
        }

        /// <summary>Records an error condition for a provider account.</summary>
        public async Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(cancellationToken);
            ProviderAccount? account = null;
            try
            {
                account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                if (account != null)
                {
                    account.LastErrorUtc = DateTime.UtcNow;
                    account.LastErrorMessage = errorMessage;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Provider account error recorded: {ProviderName}", SanitizeForLog(account.ProviderName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording error for provider account: {ProviderName}", SanitizeForLog(account?.ProviderName ?? "unknown"));
                throw;
            }
        }

        /// <summary>Records a successful authentication for a provider account, clearing any prior error state.</summary>
        public async Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(cancellationToken);
            ProviderAccount? account = null;
            try
            {
                account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                if (account != null)
                {
                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorUtc = null;
                    account.LastErrorMessage = null;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    _logger.LogDebug("Provider account success recorded, error state cleared: {ProviderName}", SanitizeForLog(account.ProviderName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording success for provider account: {ProviderName}", SanitizeForLog(account?.ProviderName ?? "unknown"));
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');

        /// <summary>A newly linked account is enabled for scheduled sync by default; an existing schedule is left alone.</summary>
        internal static async Task AddDefaultScheduleIfMissingAsync(VideoForensicsDbContext db, Guid providerAccountId, CancellationToken ct)
        {
            if (!await db.SyncSchedules.AnyAsync(s => s.ProviderAccountId == providerAccountId, ct))
            {
                _ = db.SyncSchedules.Add(SyncSchedule.CreateDefault(providerAccountId));
            }
        }
    }
}
