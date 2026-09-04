using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// The configured network access tier (plan §5.2) - Local by default, each wider tier an
    /// explicit opt-in. Gated SuperAdmin+Local like every other infrastructure-exposure screen
    /// (§5.10). Only WIDENING (Local→Network, Local→Internet, Network→Internet) requires step-up
    /// re-authentication (§5.7's own "widening the network tier" example) - narrowing back toward
    /// Local only reduces exposure, so it's allowed without a fresh passkey assertion, the same
    /// asymmetry RemoteAccessEndpoints already applies to tunnel start vs. stop. Implemented inline
    /// here rather than via the reusable StepUpEndpointFilter, since that filter is unconditional
    /// and this needs to depend on the requested value, not just the route.
    /// </summary>
    public static class NetworkSettingsEndpoints
    {
        public static void MapNetworkSettingsEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/network-settings").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            group.MapGet("/", (IForensicsConfiguration config) => Results.Ok(new
            {
                configuredTier = config.ConfiguredNetworkTier.ToString(),
                // Kestrel's actual bind addresses are fixed at process startup (Program.cs reads
                // this same setting before the host is built) - a change here only takes effect
                // after the next restart, which the UI must say plainly rather than implying it's
                // live.
                requiresRestartToTakeEffect = true
            }));

            group.MapPost("/", async (
                SetNetworkTierRequest request,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                IStepUpAuthService stepUpAuth,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                var currentTier = config.ConfiguredNetworkTier;
                var isWidening = request.Tier > currentTier;

                if (isWidening)
                {
                    var deviceIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
                    if (!Guid.TryParse(deviceIdClaim, out var pairedDeviceId))
                    {
                        return Results.Unauthorized();
                    }

                    if (!context.Request.Headers.TryGetValue("X-StepUp-Token", out var stepUpToken)
                        || !stepUpAuth.Validate(stepUpToken.ToString(), pairedDeviceId))
                    {
                        return Results.Json(
                            new { error = "Widening the network tier requires step-up re-authentication (X-StepUp-Token header missing or invalid)." },
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                }

                config.ConfiguredNetworkTier = request.Tier;
                await configService.SaveConfigurationAsync(config, ct);

                var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await auditLog.LogAsync(SecurityAuditEventTypes.NetworkTierChanged,
                    Guid.TryParse(operatorIdClaim, out var actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"{currentTier} -> {request.Tier}", isUrgent: true, ct);

                return Results.Ok(new { requiresRestartToTakeEffect = true });
            });
        }
    }

    public record SetNetworkTierRequest(NetworkTier Tier);
}
