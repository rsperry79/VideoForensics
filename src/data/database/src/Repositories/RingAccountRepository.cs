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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RingAccountRepository> _logger;

        public RingAccountRepository(IUnitOfWork unitOfWork, ILogger<RingAccountRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public Task<RingAccount?> GetAsync(Guid id, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().RingAccounts.FirstOrDefaultAsync(a => a.Id == id, ct), ct);
        }

        public Task<RingAccount?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().RingAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == providerAccountId, ct), ct);
        }

        public async Task AddAsync(RingAccount account, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.RingAccounts.Add(account);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Ring account added: {AccountId} (subscription: {SubscriptionLevel})",
                    account.Id, account.SubscriptionLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding Ring account: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateAsync(RingAccount account, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.RingAccounts.Update(account);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Ring account updated: {AccountId} (subscription: {SubscriptionLevel})",
                    account.Id, account.SubscriptionLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating Ring account: {AccountId} (subscription: {SubscriptionLevel}): {Message} ({ErrorType})",
                    account.Id, account.SubscriptionLevel, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            try
            {
                bool deleted = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    RingAccount? account = await db.RingAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
                    if (account == null)
                    {
                        return false;
                    }

                    _ = db.RingAccounts.Remove(account);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted)
                {
                    _logger.LogDebug("Ring account deleted: {AccountId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting Ring account: {AccountId}: {Message} ({ErrorType})", id, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
