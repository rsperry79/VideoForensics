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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PushSubscriptionRepository> _logger;

        /// <summary>Initializes a new instance of the PushSubscriptionRepository.</summary>
        public PushSubscriptionRepository(IUnitOfWork unitOfWork, ILogger<PushSubscriptionRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Adds or updates a push subscription (upsert by Endpoint). Updates LastUsedUtc if the subscription already exists.</summary>
        public async Task AddOrUpdateAsync(PushSubscription subscription, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    PushSubscription? existing = await db.PushSubscriptions
                        .FirstOrDefaultAsync(ps => ps.Endpoint == subscription.Endpoint, ct);

                    if (existing != null)
                    {
                        existing.P256dhKey = subscription.P256dhKey;
                        existing.AuthKey = subscription.AuthKey;
                        existing.LastUsedUtc = DateTime.UtcNow;
                        _ = db.PushSubscriptions.Update(existing);
                        _logger.LogDebug("Push subscription updated");
                    }
                    else
                    {
                        subscription.Id = subscription.Id == Guid.Empty ? Guid.NewGuid() : subscription.Id;
                        subscription.CreatedUtc = DateTime.UtcNow;
                        _ = await db.PushSubscriptions.AddAsync(subscription, ct);
                        _logger.LogDebug("Push subscription added");
                    }

                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding or updating push subscription: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists all active push subscriptions for an operator.</summary>
        public async Task<IReadOnlyList<PushSubscription>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ReadAsync(context => context.Db().PushSubscriptions
                    .Where(ps => ps.OperatorId == operatorId)
                    .ToListAsync(ct), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error listing push subscriptions for operator: {OperatorId}: {Message} ({ErrorType})", operatorId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Lists push subscriptions for operators whose role is Admin or above and who are both active and approved
        /// (for system-wide notifications). Filters on the owning operator, not on the subscription, because the
        /// role, active, and approval flags are stored on Operator.
        /// </summary>
        public async Task<IReadOnlyList<PushSubscription>> ListForAdminsAsync(CancellationToken ct)
        {
            try
            {
                // Same "at least Admin" rule as LiveHub (role >= OperatorRole.Admin). Deactivated or unapproved
                // operators are excluded so they do not receive admin-only security pushes. Using a subquery keeps
                // the result limited to PushSubscription rows, with no duplicates from a join.
                return await _unitOfWork.ReadAsync(context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    return db.PushSubscriptions
                        .Where(ps => db.Operators.Any(o => o.Id == ps.OperatorId && o.Role >= OperatorRole.Admin && o.Active && o.IsApproved))
                        .ToListAsync(ct);
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error listing push subscriptions for admins: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists all push subscriptions across operators (for All-audience notifications).</summary>
        public async Task<IReadOnlyList<PushSubscription>> ListAllAsync(CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ReadAsync(context => context.Db().PushSubscriptions.ToListAsync(ct), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error listing push subscriptions for all operators: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Removes a push subscription by endpoint URL (e.g., when the browser unsubscribes or the subscription is invalid).</summary>
        public async Task RemoveAsync(string endpoint, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    PushSubscription? subscription = await db.PushSubscriptions
                        .FirstOrDefaultAsync(ps => ps.Endpoint == endpoint, ct);

                    if (subscription != null)
                    {
                        _ = db.PushSubscriptions.Remove(subscription);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Push subscription removed");
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error removing push subscription: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
