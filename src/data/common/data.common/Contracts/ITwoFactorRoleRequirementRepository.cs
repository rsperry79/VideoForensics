using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for TwoFactorRoleRequirement, per-role two-factor authentication requirement configuration.</summary>
    public interface ITwoFactorRoleRequirementRepository
    {
        /// <summary>
        /// Retrieves all TwoFactorRoleRequirement rows (one per OperatorRole: ReadOnly, Review, Admin, SuperAdmin).
        /// If the table is empty (never seeded), synthesizes one row per OperatorRole with RequireTwoFactor=true (the secure-by-default).
        /// Callers always receive a complete set of four roles; empty database is filled with secure defaults, not an empty list.
        /// </summary>
        Task<IReadOnlyList<TwoFactorRoleRequirement>> GetAllAsync(CancellationToken ct);

        /// <summary>
        /// Convenience lookup: returns the RequireTwoFactor flag for a specific OperatorRole.
        /// Falls back to true if no row exists for that role (secure-by-default).
        /// </summary>
        Task<bool> GetRequirementForRoleAsync(OperatorRole role, CancellationToken ct);

        /// <summary>
        /// Inserts or updates a TwoFactorRoleRequirement row by Role (unique index enforces one row per role).
        /// If a row for that role already exists, updates it; otherwise, creates a new one.
        /// </summary>
        Task UpsertAsync(TwoFactorRoleRequirement requirement, CancellationToken ct);
    }
}
