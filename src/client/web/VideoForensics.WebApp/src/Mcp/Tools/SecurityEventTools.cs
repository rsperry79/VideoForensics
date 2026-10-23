using ModelContextProtocol.Server;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Mcp.Tools
{
    /// <summary>Phase 0.5 security tools: operator authentication and account security event visibility</summary>
    [McpServerToolType]
    public class SecurityEventTools : ForensicsToolBase
    {
        private readonly ISecurityAuditService _auditService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMcpCallerAuthorizer _authorizer;

        public SecurityEventTools(
            ISecurityAuditService auditService,
            IHttpContextAccessor httpContextAccessor,
            IMcpCallerAuthorizer authorizer,
            ILogger<SecurityEventTools> logger)
            : base(logger)
        {
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
            _authorizer = authorizer;
        }

        /// <summary>
        /// Retrieve security events (login attempts, breaches, account lockouts) for visibility and audit trails.
        /// When operator_id is omitted, returns the caller's own events (self-service).
        /// When operator_id is present, requires SuperAdminLocalOrPasskey authorization and returns cross-account audit data.
        /// </summary>
        [McpServerTool]
        public async Task<SecurityEventsResult> GetSecurityEvents(
            int limit = 20,
            int offset = 0,
            string? operatorId = null,
            CancellationToken cancellationToken = default)
        {
            HttpContext? context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return new SecurityEventsResult { Events = [], TotalCount = 0, Error = "HTTP context not available" };
            }

            // Validate limit
            if (limit is < 1 or > 1000)
            {
                limit = 20;
            }

            if (offset < 0)
            {
                offset = 0;
            }

            // Extract caller's OperatorId from claims
            string? callerIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(callerIdClaim, out Guid callerId))
            {
                return new SecurityEventsResult { Events = [], TotalCount = 0, Error = "Caller authentication missing or invalid" };
            }

            // Determine if this is a self-service query (no operatorId) or cross-account (operatorId present)
            Guid targetOperatorId;
            bool isSelfService;

            if (string.IsNullOrEmpty(operatorId))
            {
                // Self-service: caller queries their own events
                targetOperatorId = callerId;
                isSelfService = true;
            }
            else
            {
                // Cross-account query: verify SuperAdminLocalOrPasskey authorization
                if (!Guid.TryParse(operatorId, out Guid parsedId))
                {
                    return new SecurityEventsResult { Events = [], TotalCount = 0, Error = "Invalid operatorId format" };
                }

                targetOperatorId = parsedId;
                isSelfService = false;

                // Cross-account access is SuperAdminLocalOrPasskey, the same policy as the HTTP endpoint. Checking the role
                // alone would let a remote SuperAdmin read other operators' security events.
                if (!await _authorizer.IsAllowedAsync(VideoForensicsPolicies.SuperAdminLocalOrPasskey))
                {
                    Logger.LogWarning("GetSecurityEvents: cross-account query denied; caller {CallerId} lacks SuperAdmin", callerId);
                    return new SecurityEventsResult { Events = [], TotalCount = 0, Error = "Unauthorized: SuperAdmin required for cross-account queries" };
                }

                Logger.LogDebug("GetSecurityEvents: cross-account query from caller {CallerId} for operator {TargetId}", callerId, targetOperatorId);
            }

            Logger.LogDebug("GetSecurityEvents: limit={Limit}, offset={Offset}, operatorId={OperatorId}, self-service={IsSelfService}, caller={CallerId}",
                limit, offset, targetOperatorId, isSelfService, callerId);

            try
            {
                // Fetch events
                var events = new List<SecurityEventDto>();
                await foreach (SecurityEventDto @event in _auditService.GetOperatorEventsAsync(targetOperatorId, offset, limit, cancellationToken))
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
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetSecurityEvents: service error for operatorId={OperatorId}", targetOperatorId);
                return new SecurityEventsResult { Events = [], TotalCount = 0, Error = $"Service error: {ex.Message}" };
            }
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
