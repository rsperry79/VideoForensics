using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
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
            RouteGroupBuilder group = app.MapGroup("/api/devices-management").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/paired-devices", async (IPairedDeviceRepository devices, CancellationToken ct) =>
                Results.Ok(await devices.ListAsync(ct)));

            _ = group.MapGet("/operators", async (IOperatorRepository operators, CancellationToken ct) =>
                Results.Ok(await operators.ListAsync(ct)));

            _ = group.MapPost("/paired-devices/{id:guid}/revoke", async (
                Guid id,
                RevokeDeviceRequest request,
                IPairedDeviceRepository devices,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                ILiveConnectionTracker connectionTracker,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await devices.RevokeAsync(id, request.Reason, ct);
                connectionTracker.ForceDisconnect(id);
                await auditLog.LogAsync(SecurityAuditEventTypes.PairingRevoked,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    id, tierResolver.ResolveClientIp(context), request.Reason, isUrgent: true, ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/operators/{id:guid}/deactivate", async (
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
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await operators.DeactivateAsync(id, ct);
                IReadOnlyList<Guid> revokedDeviceIds = await devices.RevokeAllForOperatorAsync(id, request.Reason, ct);
                foreach (Guid revokedDeviceId in revokedDeviceIds)
                {
                    connectionTracker.ForceDisconnect(revokedDeviceId);
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.OperatorDeactivated,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Operator {id}, {revokedDeviceIds.Count} device(s) revoked: {request.Reason}", isUrgent: true, ct);

                return Results.Ok(new { revokedDeviceCount = revokedDeviceIds.Count });
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/operators/{id:guid}/approve", async (
                Guid id,
                IOperatorRepository operators,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await operators.ApproveAsync(id, ct);
                await auditLog.LogAsync(SecurityAuditEventTypes.OperatorApproved,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Operator {id} approved", isUrgent: true, ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/operators/{id:guid}/reset-password", async (
                Guid id,
                IOperatorRepository operators,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;

                // Generate a temporary password (12 bytes of random data, base64url encoded)
                string temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
                    .TrimEnd('=')  // Remove padding
                    .Replace('+', '-').Replace('/', '_');  // URL-safe base64

                var hasher = new PasswordHasher<Operator>();
                // Create a minimal temp operator instance (PasswordHasher only uses it for type checking)
                var tempOperator = new Operator
                {
                    Id = id,
                    DisplayName = string.Empty,
                    Username = string.Empty,
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    Email = string.Empty
                };
                string passwordHash = hasher.HashPassword(tempOperator, temporaryPassword);

                await operators.SetPasswordAsync(id, passwordHash, mustChangePassword: true, ct);
                await auditLog.LogAsync(SecurityAuditEventTypes.PasswordReset,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Password reset for operator {id}", isUrgent: true, ct);

                return Results.Ok(new { temporaryPassword });
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/operators/{id:guid}/unlock", UnlockAsync);

            // Separate route group for operator credentials management. Base policy is the LOWEST
            // requirement any route here needs (ReadOnly, i.e. "just signed in") - RequireAuthorization
            // calls stack additively (AND-combined) rather than replacing each other, so a stricter
            // group-level policy here would make it impossible for a per-route override to LOOSEN
            // access back down for the self-service routes (/mine, /revoke's self-revoke path).
            // Routes that need more than ReadOnly add their own stricter policy on top instead.
            RouteGroupBuilder credentialGroup = app.MapGroup("/api/devices-management/operator-credentials").RequireAuthorization(VideoForensicsPolicies.ReadOnly);

            _ = credentialGroup.MapGet("/pending", async (
                IOperatorCredentialRepository credentials,
                CancellationToken ct) =>
                Results.Ok(await credentials.ListPendingApprovalAsync(ct)))
            .RequireAuthorization(VideoForensicsPolicies.Admin);

            _ = credentialGroup.MapPost("/{id:guid}/approve", async (
                Guid id,
                IOperatorCredentialRepository credentials,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await credentials.ApproveAsync(id, ct);
                await auditLog.LogAsync(SecurityAuditEventTypes.CredentialApproved,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Credential {id} approved", isUrgent: true, ct);
                return Results.Ok();
            })
            .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)
            .AddEndpointFilter<StepUpEndpointFilter>();

            _ = credentialGroup.MapPost("/{id:guid}/revoke", async (
                Guid id,
                RevokeDeviceRequest request,
                IOperatorCredentialRepository credentials,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                string? tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
                string? roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;

                OperatorCredential? credential = await credentials.GetAsync(id, ct);
                if (credential == null)
                {
                    return Results.NotFound();
                }

                // Allow if: (a) self-revoking own credential, OR (b) SuperAdmin+Local
                bool isSelf = Guid.TryParse(operatorIdClaim, out Guid callerOperatorId) && callerOperatorId == credential.OperatorId;
                bool isSuperAdminLocal = Enum.TryParse<OperatorRole>(roleClaim, out OperatorRole role) && role >= OperatorRole.SuperAdmin
                    && Enum.TryParse<NetworkTier>(tierClaim, out NetworkTier tier) && tier == NetworkTier.Local;

                if (!isSelf && !isSuperAdminLocal)
                {
                    return Results.Forbid();
                }

                await credentials.RevokeAsync(id, request.Reason, ct);
                await auditLog.LogAsync(SecurityAuditEventTypes.CredentialRevoked,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Credential {id} revoked: {request.Reason}", isUrgent: true, ct);
                return Results.Ok();
            })
            .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
            .AddEndpointFilter<StepUpEndpointFilter>();

            _ = credentialGroup.MapGet("/mine", async (
                IOperatorCredentialRepository credentials,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await credentials.ListForOperatorAsync(operatorId, ct));
            })
            .RequireAuthorization(VideoForensicsPolicies.ReadOnly);
        }

        /// <summary>
        /// Clears an operator's lockout state. Unlike approve/deactivate this restores existing
        /// access rather than granting new access, so it deliberately carries no step-up requirement.
        /// </summary>
        private static async Task<IResult> UnlockAsync(
            Guid id,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await operators.UnlockAsync(id, ct);
            await auditLog.LogAsync(SecurityAuditEventTypes.OperatorUnlocked,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"Operator {id} unlocked", isUrgent: true, ct);
            return Results.Ok();
        }
    }

    public record RevokeDeviceRequest(string Reason);
}
