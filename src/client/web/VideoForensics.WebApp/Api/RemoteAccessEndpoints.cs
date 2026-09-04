using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Cloudflare Tunnel management for the Remote Access screen (plan §5.3). Gated
    /// SuperAdmin+Local like every other infrastructure-exposure screen (§5.10) - and, since
    /// starting a tunnel is functionally "widen the network tier to Internet" even though the
    /// separate tier-setting UI from §5.2 doesn't exist yet, START additionally requires step-up
    /// re-authentication (§5.7's "widening the network tier" case) even within an already-verified
    /// SuperAdminLocal session. Stopping a tunnel only reduces exposure, so it does not.
    /// </summary>
    public static class RemoteAccessEndpoints
    {
        public static void MapRemoteAccessEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/remote-access").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            group.MapGet("/status", async (ICloudflaredTunnelService tunnels, CancellationToken ct) =>
            {
                var installed = await tunnels.IsInstalledAsync(ct);
                var state = tunnels.GetState();
                return Results.Ok(new
                {
                    installed,
                    kind = state.Kind.ToString(),
                    status = state.Status.ToString(),
                    publicUrl = state.PublicUrl,
                    errorMessage = state.ErrorMessage,
                    recentLogLines = state.RecentLogLines
                });
            });

            group.MapGet("/named-tunnels", async (ICloudflaredTunnelService tunnels, CancellationToken ct) =>
                Results.Ok(await tunnels.ListNamedTunnelsAsync(ct)));

            group.MapPost("/quick-tunnel/start", async (
                ICloudflaredTunnelService tunnels,
                IServer server,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                var port = ResolveListeningPort(server);
                if (port is null)
                {
                    return Results.Json(new { error = "Could not determine the server's own listening port." }, statusCode: StatusCodes.Status500InternalServerError);
                }

                await tunnels.StartQuickTunnelAsync(port.Value, ct);
                await LogTunnelEventAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.TunnelStarted, "quick tunnel", ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            group.MapPost("/named-tunnel/start", async (
                StartNamedTunnelRequest request,
                ICloudflaredTunnelService tunnels,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest(new { error = "Tunnel name is required." });
                }

                await tunnels.StartNamedTunnelAsync(request.Name, ct);
                await LogTunnelEventAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.TunnelStarted, $"named tunnel '{request.Name}'", ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            group.MapPost("/stop", async (
                ICloudflaredTunnelService tunnels,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                await tunnels.StopAsync(ct);
                await LogTunnelEventAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.TunnelStopped, null, ct);
                return Results.Ok();
            });
        }

        private static Task LogTunnelEventAsync(
            ISecurityAuditLogger auditLog, INetworkTierResolver tierResolver, HttpContext context, string eventType, string? details, CancellationToken ct)
        {
            var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return auditLog.LogAsync(eventType,
                Guid.TryParse(operatorIdClaim, out var operatorId) ? operatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);
        }

        private static int? ResolveListeningPort(IServer server)
        {
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            if (addresses is null)
            {
                return null;
            }

            foreach (var address in addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
                {
                    return uri.Port;
                }
            }

            return null;
        }
    }

    public record StartNamedTunnelRequest(string Name);
}
