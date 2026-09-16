using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for Web Push API subscription credentials (VAPID-based), enabling push notifications to browser/mobile clients.</summary>
    public interface IPushSubscriptionRepository
    {
        /// <summary>Adds or updates a push subscription (upsert by Endpoint). Updates LastUsedUtc if the subscription already exists.</summary>
        Task AddOrUpdateAsync(PushSubscription subscription, CancellationToken ct);

        /// <summary>Lists all active push subscriptions for an operator.</summary>
        Task<IReadOnlyList<PushSubscription>> ListForOperatorAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Lists all push subscriptions for operators with Admin+ role (for system-wide notifications).</summary>
        Task<IReadOnlyList<PushSubscription>> ListForAdminsAsync(CancellationToken ct);

        /// <summary>Removes a push subscription by endpoint URL (e.g., when the browser unsubscribes or the subscription is invalid).</summary>
        Task RemoveAsync(string endpoint, CancellationToken ct);
    }
}
