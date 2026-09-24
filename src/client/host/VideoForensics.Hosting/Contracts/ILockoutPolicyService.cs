using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Service for managing account lockout policy settings (maximum failed login attempts, lockout duration, etc).
    /// On a client host (MAUI), this is backed by HTTP calls to the server.
    /// </summary>
    public interface ILockoutPolicyService
    {
        /// <summary>
        /// Retrieves the current lockout policy settings from the server.
        /// </summary>
        Task<LockoutPolicySettings> GetAsync(CancellationToken ct);

        /// <summary>
        /// Updates the lockout policy settings on the server.
        /// </summary>
        Task UpdateAsync(LockoutPolicySettings settings, CancellationToken ct);
    }
}
