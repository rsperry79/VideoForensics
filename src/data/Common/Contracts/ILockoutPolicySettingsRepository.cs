using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for LockoutPolicySettings, a singleton account lockout configuration entity.</summary>
    public interface ILockoutPolicySettingsRepository
    {
        /// <summary>
        /// Retrieves the singleton LockoutPolicySettings row.
        /// If no row exists yet, returns a new instance with default values (does not persist).
        /// This ensures callers always get a complete settings object for display or initialization.
        /// </summary>
        Task<LockoutPolicySettings> GetAsync(CancellationToken ct);

        /// <summary>
        /// Inserts or updates the singleton LockoutPolicySettings row.
        /// If no row exists, creates one; if one exists, updates all fields.
        /// </summary>
        Task UpsertAsync(LockoutPolicySettings settings, CancellationToken ct);
    }
}
