using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Hubs;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Paired-device and Operator management (plan §5.4/§5.11): listing, revoking a single device,
    /// and deactivating an Operator (bulk-revoking every device they've paired). All gated
    /// SuperAdmin+Local - revocation is exactly as sensitive as pairing itself.
    ///
    /// Revocation here does two things, matching the plan's explicit "not just the next HTTP
    /// request" requirement: it immediately blocks the device's next API request (enforced by
    /// PairedDeviceAuthenticationHandler re-checking IsActive on every call), AND it forcibly
    /// terminates any already-open LiveHub connection for that device via ILiveConnectionTracker -
    /// token invalidation alone does nothing to a persistent SignalR connection that was
    /// established before the revocation.
    /// </summary>
    public static class DeviceManagementEndpoints
    {
        public static void MapDeviceManagementEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/devices-management").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            group.MapGet("/paired-devices", async (IPairedDeviceRepository devices, CancellationToken ct) =>
                Results.Ok(await devices.ListAsync(ct)));

            group.MapGet("/operators", async (IOperatorRepository operators, CancellationToken ct) =>
                Results.Ok(await operators.ListAsync(ct)));

            group.MapPost("/paired-devices/{id:guid}/revoke", async (
                Guid id,
                RevokeDeviceRequest request,
                IPairedDeviceRepository devices,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                ILiveConnectionTracker connectionTracker,
                HttpContext context,
                CancellationToken ct) =>
            {
                var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await devices.RevokeAsync(id, request.Reason, ct);
                connectionTracker.ForceDisconnect(id);
                await auditLog.LogAsync(SecurityAuditEventTypes.PairingRevoked,
                    Guid.TryParse(operatorIdClaim, out var actingOperatorId) ? actingOperatorId : null,
                    id, tierResolver.ResolveClientIp(context), request.Reason, isUrgent: true, ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            group.MapPost("/operators/{id:guid}/deactivate", async (
                Guid id,
                RevokeDeviceRequest request,
                IOperatorRepository operators,
                IPairedDeviceRepository devices,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                ILiveConnectionTracker connectionTracker,
                HttpContext context,
                CancellationToken ct) =>
            {
                var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await operators.DeactivateAsync(id, ct);
                var revokedDeviceIds = await devices.RevokeAllForOperatorAsync(id, request.Reason, ct);
                foreach (var revokedDeviceId in revokedDeviceIds)
                {
                    connectionTracker.ForceDisconnect(revokedDeviceId);
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.OperatorDeactivated,
                    Guid.TryParse(operatorIdClaim, out var actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Operator {id}, {revokedDeviceIds.Count} device(s) revoked: {request.Reason}", isUrgent: true, ct);

                return Results.Ok(new { revokedDeviceCount = revokedDeviceIds.Count });
            }).AddEndpointFilter<StepUpEndpointFilter>();
        }
    }

    public record RevokeDeviceRequest(string Reason);
}
