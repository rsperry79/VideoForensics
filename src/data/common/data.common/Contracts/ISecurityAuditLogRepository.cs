using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for SecurityAuditLogEntry rows (plan §5.5). Append-only in practice - no update/delete methods are exposed.</summary>
    public interface ISecurityAuditLogRepository
    {
        Task<SecurityAuditLogEntry> AppendAsync(SecurityAuditLogEntry entry, CancellationToken ct);

        /// <summary>Lists entries, most recent first, optionally filtered by Operator (plan §5.5's "filterable by Operator" requirement).</summary>
        Task<IReadOnlyList<SecurityAuditLogEntry>> ListAsync(Guid? operatorId, int maxResults, CancellationToken ct);
    }
}
