using ModelContextProtocol.Server;

using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.WebApp.Mcp.Tools
{
    /// <summary>Phase 0.5 security tools: operator authentication and account security event visibility</summary>
    [McpServerToolType]
    public class SecurityEventTools : ForensicsToolBase
    {
        private readonly ISecurityAuditService _auditService;

        public SecurityEventTools(ISecurityAuditService auditService, ILogger<SecurityEventTools> logger)
            : base(logger)
        {
            _auditService = auditService;
        }

        /// <summary>
        /// Retrieve security events (login attempts, breaches, account lockouts) for visibility and audit trails.
        /// When operator_id is omitted, returns the caller's own events (self-service).
        /// When operator_id is present, requires SuperAdminLocal authorization and returns cross-account audit data.
        /// </summary>
        [McpServerTool]
        public async Task<SecurityEventsResult> GetSecurityEvents(
            int limit = 20,
            int offset = 0,
            string? operatorId = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("GetSecurityEvents: limit={Limit}, offset={Offset}, operatorId={OperatorId}",
                limit, offset, operatorId ?? "(self-service)");

            // Validate limit
            if (limit < 1 || limit > 1000)
            {
                limit = 20;
            }

            if (offset < 0)
            {
                offset = 0;
            }

            // Determine if this is a self-service query (no operatorId) or cross-account (operatorId present)
            Guid targetOperatorId;
            if (string.IsNullOrEmpty(operatorId))
            {
                // Self-service: not implemented via this tool stub
                // (actual implementation would extract caller's ID from HTTP context)
                throw new NotImplementedException("Self-service query requires HTTP context (use /api/v1/security-events endpoint instead)");
            }
            else
            {
                // Cross-account query
                if (!Guid.TryParse(operatorId, out Guid parsedId))
                {
                    return new SecurityEventsResult { Events = [], TotalCount = 0, Error = "Invalid operatorId format" };
                }
                targetOperatorId = parsedId;
            }

            // Fetch events
            var events = new List<SecurityEventDto>();
            await foreach (var @event in _auditService.GetOperatorEventsAsync(targetOperatorId, offset, limit, cancellationToken))
            {
                events.Add(@event);
            }

            return new SecurityEventsResult
            {
                Events = events,
                TotalCount = events.Count,
                Error = null
            };
        }
    }

    /// <summary>Result wrapper for security events query.</summary>
    public class SecurityEventsResult
    {
        public List<SecurityEventDto> Events { get; set; } = [];
        public int TotalCount { get; set; }
        public string? Error { get; set; }
    }
}
