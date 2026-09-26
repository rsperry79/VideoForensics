using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Operator security event query endpoints (Phase 0.5).
    /// Provides self-service access to an operator's own security events (login attempts, breaches, lockouts),
    /// and SuperAdminLocal cross-account audit access for administrative queries.
    /// </summary>
    public static class SecurityEventsEndpoints
    {
        public static void MapSecurityEventsEndpoints(this WebApplication app)
        {
            _ = app.MapPost("/api/v1/security-events", GetSecurityEventsAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .WithSummary("Query operator security events (self-service or cross-account audit)")
                .Produces<List<VideoForensics.Api.Contracts.SecurityEventDto>>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status500InternalServerError);
        }

        private static async Task<IResult> GetSecurityEventsAsync(
            SecurityEventsQueryRequest request,
            ISecurityAuditService auditService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            // Extract caller's OperatorId from claims
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out Guid callerId))
            {
                return Results.Unauthorized();
            }

            // Validate request parameters
            int limit = request.Limit ?? 20;
            int offset = request.Offset ?? 0;

            if (limit < 1 || limit > 1000)
            {
                return Results.BadRequest(new { error = "Limit must be between 1 and 1000." });
            }

            if (offset < 0)
            {
                return Results.BadRequest(new { error = "Offset must be non-negative." });
            }

            // Determine target operator ID and authorization mode
            Guid targetOperatorId;
            bool isSelfService;

            if (string.IsNullOrEmpty(request.OperatorId))
            {
                // Self-service: caller queries their own events
                targetOperatorId = callerId;
                isSelfService = true;
            }
            else
            {
                // Cross-account query: caller requests another operator's events
                if (!Guid.TryParse(request.OperatorId, out Guid parsedOperatorId))
                {
                    return Results.BadRequest(new { error = "OperatorId must be a valid UUID." });
                }

                targetOperatorId = parsedOperatorId;
                isSelfService = false;

                // Cross-account queries require SuperAdminLocal policy
                // Check: caller must be SuperAdmin AND request must have arrived via Local tier
                string? roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
                if (!Enum.TryParse<OperatorRole>(roleClaim, out OperatorRole callerRole) || callerRole != OperatorRole.SuperAdmin)
                {
                    logger.LogWarning("GetSecurityEvents: cross-account query denied; caller {CallerId} is not SuperAdmin", callerId);
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                // Read the NetworkTier claim set once by PairedDeviceAuthenticationHandler, rather
                // than re-resolving the connection's own tier here - for a self-HTTP call from the
                // WebApp's own Blazor UI, the connection is always loopback regardless of where the
                // real browser is, so re-resolving it here would silently grant Local tier to a
                // caller whose real session tier is Internet/Network.
                string? tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
                if (!Enum.TryParse<NetworkTier>(tierClaim, out NetworkTier tier) || tier != NetworkTier.Local)
                {
                    logger.LogWarning("GetSecurityEvents: cross-account query denied; caller {CallerId} is not on Local tier (tier={Tier})", callerId, tier);
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }
            }

            // Log the query
            logger.LogInformation("GetSecurityEvents: operatorId={OperatorId}, self-service={IsSelfService}, caller={CallerId}",
                targetOperatorId, isSelfService, callerId);

            try
            {
                // Fetch events from the audit service and map to API DTOs
                var events = new List<VideoForensics.Api.Contracts.SecurityEventDto>();
                await foreach (var @event in auditService.GetOperatorEventsAsync(targetOperatorId, offset, limit, ct))
                {
                    events.Add(new VideoForensics.Api.Contracts.SecurityEventDto(
                        Id: @event.Id,
                        OperatorId: @event.OperatorId,
                        EventType: @event.EventType,
                        Success: @event.Success,
                        IpAddress: @event.IpAddress,
                        OccurredAtUtc: @event.OccurredAtUtc,
                        Reason: @event.Reason
                    ));
                }

                return Results.Ok(events);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GetSecurityEvents: service error for operatorId={OperatorId}", targetOperatorId);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
