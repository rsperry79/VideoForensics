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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProviderAccountRepository> _logger;

        /// <summary>Initializes a new instance of the ProviderAccountRepository.</summary>
        public ProviderAccountRepository(IUnitOfWork unitOfWork, ILogger<ProviderAccountRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a provider account by ID.</summary>
        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct), ct);
        }

        /// <summary>Gets all provider accounts for a user.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderAccounts.Where(pa => pa.UserId == userId).ToListAsync(ct), ct);
        }

        /// <summary>Gets a provider account by user ID and provider name.</summary>
        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(
                context => context.Db().ProviderAccounts.FirstOrDefaultAsync(
                    pa => pa.UserId == userId && pa.ProviderName == providerName, ct), ct);
        }

        /// <summary>Lists all provider accounts.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderAccounts.ToListAsync(ct), ct);
        }

        /// <summary>Lists active provider accounts (excludes removed accounts).</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().ProviderAccounts.Where(pa => pa.IsActive && pa.RemovedUtc == null).ToListAsync(ct), ct);
        }

        /// <summary>Lists removed provider accounts (data retained), most recently removed first.</summary>
        public async Task<IReadOnlyList<ProviderAccount>> ListRemovedAsync(CancellationToken ct)
        {
            // SQLite cannot ORDER BY a DateTime in SQL reliably across providers, but the removed set is small; sort client-side.
            List<ProviderAccount> removed = await _unitOfWork.ReadAsync(
                context => context.Db().ProviderAccounts.Where(pa => pa.RemovedUtc != null).ToListAsync(ct), ct);
            return removed.OrderByDescending(pa => pa.RemovedUtc).ToList();
        }

        /// <summary>Retires a provider account, keeping its row and data. The step-up token is used only by the remote implementation and ignored here.</summary>
        public async Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null)
        {
            ProviderAccount? account = null;
            try
            {
                string? providerName = await _unitOfWork.ExecuteAsync<string?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                    if (account == null || account.RemovedUtc != null)
                    {
                        return null;
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
                    return account.ProviderName;
                }, ct);

                if (providerName != null)
                {
                    _logger.LogInformation("Provider account retired: {ProviderAccountId} ({ProviderName})", accountId, SanitizeForLog(providerName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retiring provider account: {ProviderAccountId} ({ProviderName}): {Message} ({ErrorType})", accountId, SanitizeForLog(account?.ProviderName ?? "unknown"), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Adds a new provider account.</summary>
        public async Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.ProviderAccounts.Add(account);
                    await AddDefaultScheduleIfMissingAsync(db, account.Id, ct);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Provider account added: {ProviderName}", SanitizeForLog(account.ProviderName));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding provider account: {ProviderName}: {Message} ({ErrorType})", SanitizeForLog(account.ProviderName), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing provider account.</summary>
        public async Task UpdateAsync(ProviderAccount account, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Provider account updated: {ProviderName}", SanitizeForLog(account.ProviderName));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating provider account: {ProviderName}: {Message} ({ErrorType})", SanitizeForLog(account.ProviderName), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes a provider account and its sync schedule in one atomic save.</summary>
        public async Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null)
        {
            ProviderAccount? account = null;
            try
            {
                string? deletedProviderName = await _unitOfWork.ExecuteAsync<string?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId, ct);
                    if (account == null)
                    {
                        return null;
                    }

                    // SyncSchedule has no FK to ProviderAccount, so nothing cascades. Remove it here or the
                    // scheduler keeps ticking an orphan and logs "Account not found" every interval.
                    SyncSchedule? schedule = await db.SyncSchedules.FirstOrDefaultAsync(s => s.ProviderAccountId == accountId, ct);
                    if (schedule != null)
                    {
                        _ = db.SyncSchedules.Remove(schedule);
                    }

                    _ = db.ProviderAccounts.Remove(account);
                    _ = await db.SaveChangesAsync(ct);
                    return account.ProviderName;
                }, ct);

                if (deletedProviderName != null)
                {
                    _logger.LogDebug("Provider account deleted: {ProviderName}", SanitizeForLog(deletedProviderName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting provider account: {ProviderName}: {Message} ({ErrorType})", SanitizeForLog(account?.ProviderName ?? "unknown"), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Records an error condition for a provider account.</summary>
        public async Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken)
        {
            ProviderAccount? account = null;
            try
            {
                string? providerName = await _unitOfWork.ExecuteAsync<string?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                    if (account == null)
                    {
                        return null;
                    }

                    account.LastErrorUtc = DateTime.UtcNow;
                    account.LastErrorMessage = errorMessage;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    return account.ProviderName;
                }, cancellationToken);

                if (providerName != null)
                {
                    _logger.LogWarning("Provider account error recorded: {ProviderName}", SanitizeForLog(providerName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording error for provider account: {ProviderName}: {Message} ({ErrorType})", SanitizeForLog(account?.ProviderName ?? "unknown"), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Records a successful authentication for a provider account, clearing any prior error state.</summary>
        public async Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            ProviderAccount? account = null;
            try
            {
                string? providerName = await _unitOfWork.ExecuteAsync<string?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, cancellationToken);
                    if (account == null)
                    {
                        return null;
                    }

                    account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                    account.LastErrorUtc = null;
                    account.LastErrorMessage = null;
                    _ = db.ProviderAccounts.Update(account);
                    _ = await db.SaveChangesAsync(cancellationToken);
                    return account.ProviderName;
                }, cancellationToken);

                if (providerName != null)
                {
                    _logger.LogDebug("Provider account success recorded, error state cleared: {ProviderName}", SanitizeForLog(providerName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording success for provider account: {ProviderName}: {Message} ({ErrorType})", SanitizeForLog(account?.ProviderName ?? "unknown"), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }

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
