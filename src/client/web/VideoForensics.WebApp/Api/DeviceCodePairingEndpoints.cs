using System.Security.Cryptography;
using System.Text;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Device-code (RFC 8628-style) pairing endpoints for headless clients (e.g. the MCP stdio bridge)
    /// that can't perform a browser-based WebAuthn ceremony (see PairingEndpoints for that flow).
    /// Issues a long-lived FallbackApiKeyHash-backed credential instead of a session token, allowing
    /// CLI tools to authenticate indefinitely without a browser.
    /// </summary>
    public static class DeviceCodePairingEndpoints
    {
        public static void MapDeviceCodePairingEndpoints(this WebApplication app)
        {
            // POST /api/v1/pairing/device-code - initiate a device-code pairing session
            _ = app.MapPost("/api/v1/pairing/device-code", async (
                HttpContext context,
                IDeviceCodePairingService deviceCodePairing,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                CancellationToken ct) =>
            {
                var session = deviceCodePairing.CreateSession();

                await auditLog.LogAsync(SecurityAuditEventTypes.DeviceCodeIssued, null, null,
                    tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);

                return Results.Ok(new
                {
                    deviceCode = session.DeviceCode,
                    userCode = session.UserCode,
                    verificationUri = "/pair/device",
                    expiresInSeconds = (int)(session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds,
                    intervalSeconds = 5
                });
            }).RequireRateLimiting("auth");

            // POST /api/v1/pairing/device-code/{userCode}/approve - approve a pending device-code session
            _ = app.MapPost("/api/v1/pairing/device-code/{userCode}/approve", async (
                string userCode,
                ApproveDeviceCodeRequest request,
                IDeviceCodePairingService deviceCodePairing,
                IOperatorRepository operators,
                IPairedDeviceRepository pairedDevices,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                // Gate: must be SuperAdmin + Local (same as pairing/initiate for non-bootstrap case)
                string? roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
                string? tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
                bool isSuperAdmin = roleClaim != null && Enum.TryParse<OperatorRole>(roleClaim, out var role) && role == OperatorRole.SuperAdmin;
                bool isLocal = tierClaim != null && Enum.TryParse<NetworkTier>(tierClaim, out var tier) && tier == NetworkTier.Local;

                if (!isSuperAdmin || !isLocal)
                {
                    return Results.Forbid();
                }

                // Look up the session by user code
                var session = deviceCodePairing.GetByUserCode(userCode);
                if (session == null || session.Status != DeviceCodePairingStatus.Pending)
                {
                    return Results.NotFound(new { error = "Pairing code expired or invalid." });
                }

                // Generate the raw API key (high-entropy, base64url-encoded)
                string rawApiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                // Hash it with SHA-256 for storage
                string apiKeyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey)));

                // Create the operator
                var op = await operators.AddAsync(new Operator
                {
                    Id = Guid.NewGuid(),
                    DisplayName = $"{request.DeviceName} (device-code)",
                    CreatedAtUtc = DateTime.UtcNow,
                    Active = true
                }, ct);

                // Create the paired device
                var pairedDevice = await pairedDevices.AddAsync(new PairedDevice
                {
                    Id = Guid.NewGuid(),
                    OperatorId = op.Id,
                    DeviceName = request.DeviceName,
                    Role = request.Role,
                    FallbackApiKeyHash = apiKeyHash,
                    PairedAtUtc = DateTime.UtcNow
                }, ct);

                // Approve the session and attach the raw key (single-use, consumed on next poll)
                deviceCodePairing.TryApprove(userCode, rawApiKey);

                // Audit log the approval
                await auditLog.LogAsync(SecurityAuditEventTypes.DeviceCodeApproved, op.Id, pairedDevice.Id,
                    tierResolver.ResolveClientIp(context), $"device={request.DeviceName}, role={request.Role}", isUrgent: true, ct);

                return Results.Ok(new
                {
                    operatorId = op.Id,
                    pairedDeviceId = pairedDevice.Id,
                    role = request.Role.ToString()
                });
            }).RequireAuthorization(policy => policy.RequireAssertion(_ => true)) // auth optional here - the handler above does the real gating
              .RequireRateLimiting("auth");

            // POST /api/v1/pairing/device-code/{deviceCode}/poll - poll for approval status and retrieve API key
            _ = app.MapPost("/api/v1/pairing/device-code/{deviceCode}/poll", (
                string deviceCode,
                IDeviceCodePairingService deviceCodePairing) =>
            {
                var session = deviceCodePairing.GetByDeviceCode(deviceCode);
                if (session == null)
                {
                    return Results.NotFound(new { status = "expired" });
                }

                if (session.Status == DeviceCodePairingStatus.Pending)
                {
                    return Results.Ok(new { status = "pending" });
                }

                if (session.Status == DeviceCodePairingStatus.Approved)
                {
                    string? apiKey = deviceCodePairing.TryConsumeApiKey(deviceCode);
                    if (apiKey != null)
                    {
                        return Results.Ok(new { status = "approved", apiKey });
                    }

                    // Key already consumed in an earlier poll
                    return Results.Ok(new { status = "expired" });
                }

                // Expired status
                return Results.NotFound(new { status = "expired" });
            }).RequireRateLimiting("auth");
        }
    }

    public record ApproveDeviceCodeRequest(string DeviceName, OperatorRole Role);
}
