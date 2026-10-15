using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Service for managing two-factor authentication policy settings (per-role requirements and per-operator overrides).
    /// On a client host (MAUI), this is backed by HTTP calls to the server.
    /// </summary>
    public interface ITwoFactorPolicyService
    {
        /// <summary>
        /// Retrieves all two-factor role requirements from the server.
        /// </summary>
        Task<IReadOnlyList<TwoFactorRoleRequirement>> GetRoleRequirementsAsync(CancellationToken ct);

        /// <summary>
        /// Updates a two-factor role requirement on the server.
        /// </summary>
        Task UpdateRoleRequirementAsync(OperatorRole role, bool requireTwoFactor, CancellationToken ct);

        /// <summary>
        /// Updates a specific operator's two-factor requirement override on the server.
        /// </summary>
        Task UpdateOperatorOverrideAsync(Guid operatorId, TwoFactorRequirementOverride @override, CancellationToken ct);
    }
}
