using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Cloudflare Tunnel management for the Remote Access screen (plan §5.3). Gated
    /// SuperAdmin+Local like every other infrastructure-exposure screen (§5.10) - and, since
    /// starting a tunnel is functionally "widen the network tier to Internet", START additionally
    /// requires step-up re-authentication (§5.7's "widening the network tier" case) even within an
    /// already-verified SuperAdminLocal session. Stopping a tunnel only reduces exposure, so it
    /// does not.
    ///
    /// Also requires the configured network tier (§5.2, now enforced at the Kestrel-bind level in
    /// Program.cs) to be at least Network before a tunnel can start - closing a real loophole: since
    /// cloudflared runs on the same machine, it reaches this server over loopback regardless of
    /// whether Kestrel itself is bound to loopback-only, so the Local-tier Kestrel restriction alone
    /// does NOT stop someone from tunneling out while "Local" is configured. This check is what
    /// actually closes that gap.
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
                IForensicsConfiguration config,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (config.ConfiguredNetworkTier == NetworkTier.Local)
                {
                    return Results.Json(new { error = "Set the network tier to Network or Internet (Network Settings) before starting a tunnel." }, statusCode: StatusCodes.Status400BadRequest);
                }

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
                IForensicsConfiguration config,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (config.ConfiguredNetworkTier == NetworkTier.Local)
                {
                    return Results.Json(new { error = "Set the network tier to Network or Internet (Network Settings) before starting a tunnel." }, statusCode: StatusCodes.Status400BadRequest);
                }

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
