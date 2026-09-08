using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for evidence access audit trail tracking. Records all user access to evidence with purpose tracking.</summary>
    public interface IAccessAuditLogRepository
    {
        /// <summary>Records a single evidence access event in the audit trail.</summary>
        /// <param name="evidenceId">The ID of the evidence that was accessed.</param>
        /// <param name="userId">The user ID of the person accessing the evidence.</param>
        /// <param name="action">The action performed: "View", "Download", "Export", etc.</param>
        /// <param name="ipAddress">The IP address from which the access occurred.</param>
        /// <param name="purpose">The stated purpose for accessing the evidence (compliance requirement).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created audit log entry.</returns>
        Task<AccessAuditLogEntity> RecordAccessAsync(
            Guid evidenceId,
            string userId,
            string action,
            string ipAddress,
            string purpose,
            CancellationToken ct);

        /// <summary>Gets a single access audit log entry by ID.</summary>
        Task<AccessAuditLogEntity?> GetAsync(Guid logId, CancellationToken ct);

        /// <summary>Gets all access records for a specific evidence item (audit trail for that evidence).</summary>
        Task<IReadOnlyList<AccessAuditLogEntity>> GetForEvidenceAsync(Guid evidenceId, CancellationToken ct);

        /// <summary>Gets all access records by a specific user (user activity history).</summary>
        Task<IReadOnlyList<AccessAuditLogEntity>> GetByUserAsync(string userId, CancellationToken ct);

        /// <summary>Gets access records within a date range (time-based audit queries).</summary>
        Task<IReadOnlyList<AccessAuditLogEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Gets all access records, optionally paginated (full audit trail export).</summary>
        Task<IReadOnlyList<AccessAuditLogEntity>> ListAsync(
            int skip = 0,
            int take = 1000,
            CancellationToken ct = default);
    }
}
