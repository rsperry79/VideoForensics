using ModelContextProtocol.Server;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Mcp.Tools
{
    /// <summary>
    /// Phase 4 forensics tools: access and export audit, chain of custody. Every tool needs Admin, the same as the HTTP
    /// security audit log, because these records show who accessed or exported evidence.
    /// </summary>
    [McpServerToolType]
    public class AuditTrailTools : ForensicsToolBase
    {
        private readonly IAuditTrailRepository _auditTrailRepository;
        private readonly IMcpCallerAuthorizer _authorizer;

        public AuditTrailTools(IAuditTrailRepository auditTrailRepository, IMcpCallerAuthorizer authorizer, ILogger<AuditTrailTools> logger) : base(logger)
        {
            _auditTrailRepository = auditTrailRepository;
            _authorizer = authorizer;
        }

        /// <summary>Get quick audit trail summary for compliance review. Use this first to assess chain of custody integrity.</summary>
        [McpServerTool]
        public async Task<AuditTrailSummary> GetAuditTrailSummary(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("GetAuditTrailSummary: location={LocationId}", locationId);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.GetAuditTrailSummaryAsync(locationId, cancellationToken);
        }

        /// <summary>Get evidence access history with full details (offset-based pagination). Use after summary indicates access concerns.</summary>
        [McpServerTool]
        public async Task<PaginatedResult<AccessAuditLog>> GetAccessHistoryPaginated(
            Guid evidenceId,
            int pageNumber = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("GetAccessHistoryPaginated: evidence={EvidenceId}, page={PageNumber}/{PageSize}",
                evidenceId, pageNumber, pageSize);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.GetAccessHistoryPaginatedAsync(evidenceId, pageNumber, pageSize, cancellationToken);
        }

        /// <summary>Stream export history with cursor pagination (for large datasets). Use cursor from previous call to continue streaming.</summary>
        [McpServerTool]
        public async Task<CursorPaginatedResult<ExportAuditRecord>> GetExportHistoryCursor(
            Guid locationId,
            string? cursor = null,
            int pageSize = 1000,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("GetExportHistoryCursor: location={LocationId}, cursor={Cursor}, pageSize={PageSize}",
                locationId, cursor ?? "null", pageSize);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.GetExportHistoryCursorAsync(locationId, cursor, pageSize, cancellationToken);
        }

        /// <summary>Verify chain of custody - confirm all accesses are logged and evidence integrity is intact.</summary>
        [McpServerTool]
        public async Task<AccessAuditReport> VerifyChainOfCustody(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("VerifyChainOfCustody: location={LocationId}", locationId);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.VerifyChainOfCustodyAsync(locationId, cancellationToken);
        }

        /// <summary>Flag unauthorized access patterns (off-hours access, excessive access, anomalies).</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<UnauthorizedAccessFlag>> FlagUnauthorizedAccess(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("FlagUnauthorizedAccess: location={LocationId}", locationId);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.FlagUnauthorizedAccessAsync(locationId, cancellationToken);
        }

        /// <summary>Get all evidence exports for a location with purpose and format details.</summary>
        [McpServerTool]
        public async Task<IReadOnlyList<ExportAuditRecord>> GetExportHistory(
            Guid locationId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("GetExportHistory: location={LocationId}", locationId);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.GetExportHistoryAsync(locationId, cancellationToken);
        }

        /// <summary>Verify export integrity - confirm exported events are unchanged.</summary>
        [McpServerTool]
        public async Task<ExportIntegrityReport> VerifyExportIntegrity(
            Guid exportId,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("VerifyExportIntegrity: exportId={ExportId}", exportId);
            await _authorizer.RequireAsync(VideoForensicsPolicies.Admin);
            return await _auditTrailRepository.VerifyExportIntegrityAsync(exportId, cancellationToken);
        }
    }
}
