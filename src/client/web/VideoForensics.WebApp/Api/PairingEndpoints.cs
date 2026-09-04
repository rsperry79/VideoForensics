using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// QR-pairing + WebAuthn passkey registration/authentication endpoints (plan §5.1). Attestation
    /// and assertion cryptographic verification is delegated entirely to Fido2NetLib - the plan
    /// explicitly flags hand-rolling this (challenge handling, attestation verification, replay
    /// protection) as easy to get subtly wrong, so it is not reimplemented here.
    ///
    /// SECURITY NOTE: like MediaApiEndpoints, these endpoints are reachable without an existing
    /// session (they ARE how a session is first established) - the pairing-initiation endpoint is
    /// the one exception that needs its own gate: it requires SuperAdmin+Local UNLESS no Operator
    /// exists yet (first-ever pairing bootstraps the initiating device as SuperAdmin, plan §5.10).
    /// </summary>
    public static class PairingEndpoints
    {
        public static void MapPairingEndpoints(this WebApplication app)
        {
            app.MapPost("/api/pairing/initiate", async (
                HttpContext context,
                IPairingTokenService pairingTokens,
                IOperatorRepository operators,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                CancellationToken ct) =>
            {
                var isBootstrap = await operators.IsEmptyAsync(ct);
                if (!isBootstrap)
                {
                    var roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
                    var tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
                    var isSuperAdmin = roleClaim != null && Enum.TryParse<OperatorRole>(roleClaim, out var role) && role == OperatorRole.SuperAdmin;
                    var isLocal = tierClaim != null && Enum.TryParse<NetworkTier>(tierClaim, out var tier) && tier == NetworkTier.Local;

                    if (!isSuperAdmin || !isLocal)
                    {
                        return Results.Forbid();
                    }
                }

                var requestedRole = isBootstrap ? OperatorRole.SuperAdmin : OperatorRole.ReadOnly;
                var info = pairingTokens.CreateToken(requestedRole);

                await auditLog.LogAsync(SecurityAuditEventTypes.PairingInitiated, null, null,
                    tierResolver.ResolveClientIp(context), $"role={requestedRole}, bootstrap={isBootstrap}", isUrgent: false, ct);

                return Results.Ok(new { token = info.Token, expiresAtUtc = info.ExpiresAtUtc, role = info.Role.ToString() });
            }).RequireAuthorization(policy => policy.RequireAssertion(_ => true)); // auth optional here - the handler above does the real gating (bootstrap case has no session at all)

            app.MapGet("/api/pairing/{token}", (string token, IPairingTokenService pairingTokens) =>
            {
                var info = pairingTokens.Peek(token);
                return info == null
                    ? Results.NotFound(new { valid = false })
                    : Results.Ok(new { valid = true, expiresAtUtc = info.ExpiresAtUtc });
            });

            app.MapPost("/api/pairing/{token}/register/options", async (
                string token,
                RegisterOptionsRequest request,
                IPairingTokenService pairingTokens,
                IWebAuthnCeremonyCache ceremonyCache,
                IFido2 fido2) =>
            {
                var info = pairingTokens.Peek(token);
                if (info == null)
                {
                    return Results.NotFound(new { error = "Pairing token expired or invalid." });
                }

                var operatorId = Guid.NewGuid();
                var fido2User = new Fido2User
                {
                    DisplayName = request.OperatorDisplayName,
                    Name = request.OperatorDisplayName,
                    Id = operatorId.ToByteArray()
                };

                var options = fido2.RequestNewCredential(new RequestNewCredentialParams
                {
                    User = fido2User,
                    ExcludeCredentials = new List<PublicKeyCredentialDescriptor>(),
                    AuthenticatorSelection = AuthenticatorSelection.Default,
                    AttestationPreference = AttestationConveyancePreference.None
                });

                var pendingRegistration = new PendingRegistration(token, operatorId, request.OperatorDisplayName, request.DeviceName, info.Role, options.ToJson());
                var nonce = ceremonyCache.Store(JsonSerializer.Serialize(pendingRegistration));

                return Results.Ok(new { nonce, options = JsonSerializer.Deserialize<JsonElement>(options.ToJson()) });
            });

            app.MapPost("/api/pairing/{token}/register/complete", async (
                string token,
                RegisterCompleteRequest request,
                IWebAuthnCeremonyCache ceremonyCache,
                IPairingTokenService pairingTokens,
                IOperatorRepository operators,
                IPairedDeviceRepository pairedDevices,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                IFido2 fido2,
                CancellationToken ct) =>
            {
                var cached = ceremonyCache.TryTake(request.Nonce);
                if (cached == null)
                {
                    return Results.BadRequest(new { error = "Registration ceremony expired or already completed." });
                }

                var pending = JsonSerializer.Deserialize<PendingRegistration>(cached)!;
                if (pending.Token != token || !pairingTokens.TryConsume(token, out _))
                {
                    return Results.BadRequest(new { error = "Pairing token mismatch or already used." });
                }

                var options = CredentialCreateOptions.FromJson(pending.OptionsJson);

                AuthenticatorAttestationRawResponse attestationResponse;
                try
                {
                    attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
                        JsonSerializer.Serialize(request.AttestationResponse))!;
                }
                catch (JsonException)
                {
                    // Never leak internal deserialization details (property names, stack trace) to
                    // the caller - a malformed client payload is an ordinary 400, not a 500.
                    return Results.BadRequest(new { error = "Malformed attestation response." });
                }

                Fido2NetLib.Objects.RegisteredPublicKeyCredential credential;
                try
                {
                    credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
                    {
                        AttestationResponse = attestationResponse,
                        OriginalOptions = options,
                        IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true)
                    }, ct);
                }
                catch (Fido2VerificationException ex)
                {
                    return Results.BadRequest(new { error = $"Passkey registration failed verification: {ex.Message}" });
                }

                var op = await operators.AddAsync(new Operator
                {
                    Id = pending.OperatorId,
                    DisplayName = pending.OperatorDisplayName,
                    CreatedAtUtc = DateTime.UtcNow,
                    Active = true
                }, ct);

                var pairedDevice = await pairedDevices.AddAsync(new PairedDevice
                {
                    Id = Guid.NewGuid(),
                    OperatorId = op.Id,
                    DeviceName = pending.DeviceName,
                    Role = pending.Role,
                    WebAuthnCredentialId = Convert.ToBase64String(credential.Id),
                    WebAuthnPublicKey = credential.PublicKey,
                    WebAuthnSignCount = credential.SignCount,
                    PairedAtUtc = DateTime.UtcNow
                }, ct);

                await auditLog.LogAsync(SecurityAuditEventTypes.PairingCompleted, op.Id, pairedDevice.Id,
                    tierResolver.ResolveClientIp(context), $"device={pending.DeviceName}, role={pending.Role}", isUrgent: true, ct);

                return Results.Ok(new { operatorId = op.Id, pairedDeviceId = pairedDevice.Id, role = pending.Role.ToString() });
            });

            app.MapPost("/api/auth/webauthn/assertion-options", async (
                IPairedDeviceRepository pairedDevices,
                IWebAuthnCeremonyCache ceremonyCache,
                IFido2 fido2,
                CancellationToken ct) =>
            {
                var allDevices = await pairedDevices.ListAsync(ct);
                var allowedCredentials = allDevices
                    .Where(d => d.IsActive && d.WebAuthnCredentialId != null)
                    .Select(d => new PublicKeyCredentialDescriptor(
                        PublicKeyCredentialType.PublicKey,
                        Convert.FromBase64String(d.WebAuthnCredentialId!),
                        null))
                    .ToList();

                var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
                {
                    AllowedCredentials = allowedCredentials,
                    UserVerification = UserVerificationRequirement.Preferred
                });
                var nonce = ceremonyCache.Store(options.ToJson());

                return Results.Ok(new { nonce, options = JsonSerializer.Deserialize<JsonElement>(options.ToJson()) });
            });

            app.MapPost("/api/auth/webauthn/assertion-complete", async (
                AssertionCompleteRequest request,
                IWebAuthnCeremonyCache ceremonyCache,
                IPairedDeviceRepository pairedDevices,
                ISessionTokenService sessionTokens,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                IFido2 fido2,
                CancellationToken ct) =>
            {
                var cachedOptionsJson = ceremonyCache.TryTake(request.Nonce);
                if (cachedOptionsJson == null)
                {
                    return Results.BadRequest(new { error = "Authentication ceremony expired." });
                }

                var options = AssertionOptions.FromJson(cachedOptionsJson);

                AuthenticatorAssertionRawResponse assertionResponse;
                try
                {
                    assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
                        JsonSerializer.Serialize(request.AssertionResponse))!;
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "Malformed assertion response." });
                }

                var credentialIdB64 = Convert.ToBase64String(assertionResponse.RawId);
                var device = await pairedDevices.GetByWebAuthnCredentialIdAsync(credentialIdB64, ct);
                if (device == null || device.WebAuthnPublicKey == null)
                {
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                        tierResolver.ResolveClientIp(context), "Unknown or revoked credential", isUrgent: true, ct);
                    return Results.Unauthorized();
                }

                Fido2NetLib.Objects.VerifyAssertionResult result;
                try
                {
                    result = await fido2.MakeAssertionAsync(new MakeAssertionParams
                    {
                        AssertionResponse = assertionResponse,
                        OriginalOptions = options,
                        StoredPublicKey = device.WebAuthnPublicKey,
                        StoredSignatureCounter = device.WebAuthnSignCount,
                        IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true)
                    }, ct);
                }
                catch (Fido2VerificationException ex)
                {
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, device.OperatorId, device.Id,
                        tierResolver.ResolveClientIp(context), ex.Message, isUrgent: true, ct);
                    return Results.Unauthorized();
                }

                var tier = tierResolver.ResolveTier(context);
                await pairedDevices.RecordSuccessfulAuthAsync(device.Id, result.SignCount, tierResolver.ResolveClientIp(context), tier, ct);

                var token = sessionTokens.Issue(device.OperatorId, device.Id, device.Role);

                await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, device.OperatorId, device.Id,
                    tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);
                await auditLog.LogAsync(SecurityAuditEventTypes.SessionVerified, device.OperatorId, device.Id,
                    tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);

                return Results.Ok(new { sessionToken = token, operatorId = device.OperatorId, role = device.Role.ToString() });
            });
        }
    }

    public record RegisterOptionsRequest(string OperatorDisplayName, string DeviceName);
    public record RegisterCompleteRequest(string Nonce, JsonElement AttestationResponse);
    public record AssertionCompleteRequest(string Nonce, JsonElement AssertionResponse);

    internal record PendingRegistration(string Token, Guid OperatorId, string OperatorDisplayName, string DeviceName, OperatorRole Role, string OptionsJson);
}
