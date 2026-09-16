using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for PushSubscription entities (Web Push API credentials per browser/device).</summary>
    public class PushSubscriptionRepository : IPushSubscriptionRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<PushSubscriptionRepository> _logger;

        /// <summary>Initializes a new instance of the PushSubscriptionRepository.</summary>
        public PushSubscriptionRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<PushSubscriptionRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Adds or updates a push subscription (upsert by Endpoint). Updates LastUsedUtc if the subscription already exists.</summary>
        public async Task AddOrUpdateAsync(PushSubscription subscription, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                PushSubscription? existing = await db.PushSubscriptions
                    .FirstOrDefaultAsync(ps => ps.Endpoint == subscription.Endpoint, ct);

                if (existing != null)
                {
                    existing.P256dhKey = subscription.P256dhKey;
                    existing.AuthKey = subscription.AuthKey;
                    existing.LastUsedUtc = DateTime.UtcNow;
                    _ = db.PushSubscriptions.Update(existing);
                    _logger.LogInformation("Push subscription updated: {Endpoint}", subscription.Endpoint);
                }
                else
                {
                    subscription.Id = subscription.Id == Guid.Empty ? Guid.NewGuid() : subscription.Id;
                    subscription.CreatedUtc = DateTime.UtcNow;
                    _ = await db.PushSubscriptions.AddAsync(subscription, ct);
                    _logger.LogInformation("Push subscription added: {Endpoint}", subscription.Endpoint);
                }

                _ = await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding or updating push subscription: {Endpoint}", subscription.Endpoint);
                throw;
            }
        }

        /// <summary>Lists all active push subscriptions for an operator.</summary>
        public async Task<IReadOnlyList<PushSubscription>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                return await db.PushSubscriptions
                    .Where(ps => ps.OperatorId == operatorId)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing push subscriptions for operator: {OperatorId}", operatorId);
                throw;
            }
        }

        /// <summary>Lists all push subscriptions for operators with Admin+ role (for system-wide notifications).</summary>
        public async Task<IReadOnlyList<PushSubscription>> ListForAdminsAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                // For now, we return all subscriptions. In a full implementation, this would filter
                // by operator role via a join to OperatorRole or similar. See INoticeRepository.ListForOperatorAsync
                // for the same pattern/note.
                return await db.PushSubscriptions.ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing push subscriptions for admins");
                throw;
            }
        }

        /// <summary>Removes a push subscription by endpoint URL (e.g., when the browser unsubscribes or the subscription is invalid).</summary>
        public async Task RemoveAsync(string endpoint, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                PushSubscription? subscription = await db.PushSubscriptions
                    .FirstOrDefaultAsync(ps => ps.Endpoint == endpoint, ct);

                if (subscription != null)
                {
                    _ = db.PushSubscriptions.Remove(subscription);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Push subscription removed: {Endpoint}", endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing push subscription: {Endpoint}", endpoint);
                throw;
            }
        }
    }
}
