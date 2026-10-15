using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-operator push notification settings - one row per OperatorId (upsert semantics).</summary>
    public interface IOperatorNotificationPreferenceRepository
    {
        /// <summary>Gets the notification preferences for an operator, or null if not yet set.</summary>
        Task<OperatorNotificationPreference?> GetAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Inserts or updates the notification preferences for the given operator (keyed by OperatorId).</summary>
        Task UpsertAsync(OperatorNotificationPreference preferences, CancellationToken ct);
    }
}
