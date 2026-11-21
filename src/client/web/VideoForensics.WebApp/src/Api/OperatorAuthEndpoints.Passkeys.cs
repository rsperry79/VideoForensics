using Fido2NetLib;
using Fido2NetLib.Objects;

using Microsoft.AspNetCore.Identity;

using System.Net;
using System.Security.Claims;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    public static partial class OperatorAuthEndpoints
    {
        /// <summary>
        /// The passkeys offered at username sign-in: approved and active only, most recently used first. Every such passkey is
        /// kept, so an operator with several is never locked out; the order only puts the last one used first.
        /// </summary>
        internal static List<PublicKeyCredentialDescriptor> UsernameAllowList(IEnumerable<OperatorCredential> credentials, bool allCredentials = false)
        {
            return credentials
                .Where(c => c.IsApproved && c.IsActive)
                .OrderByDescending(c => c.LastUsedAtUtc ?? DateTime.MinValue)
                // Single-passkey mode offers only the most recent one; the fallback offers all of them.
                .Take(allCredentials ? int.MaxValue : 1)
                .Select(c => new PublicKeyCredentialDescriptor(
                    PublicKeyCredentialType.PublicKey,
                    Convert.FromBase64String(c.WebAuthnCredentialId),
                    new[] { AuthenticatorTransport.Internal }))
                .ToList();
        }

        private static async Task<IResult> UsernameAssertionOptionsAsync(
            UsernameAssertionOptionsRequest request,
            IOperatorWriteRepository operators,
            IOperatorCredentialRepository credentials,
            IWebAuthnCeremonyCache ceremonyCache,
            ILogger<Program> logger,
            IFido2 fido2,
            CancellationToken ct)
        {
            // Passkey sign-in is username-first: the name is required, so a failed attempt can be recorded against it.
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return Results.BadRequest(new { error = "Enter your username first." });
            }

            // Look up operator - but don't reveal whether they exist (anti-enumeration)
            Operator? op = await operators.GetByUsernameAsync(request.Username, ct);

            List<PublicKeyCredentialDescriptor> allowedCredentials = [];
            if (op != null)
            {
                // Build AllowedCredentials from approved, active credentials only, most recently used first. The list still holds
                // every credential so an operator with several passkeys is never locked out; the order only favours the last one used.
                IReadOnlyList<OperatorCredential> opCredentials = await credentials.ListForOperatorAsync(op.Id, ct);
                allowedCredentials = UsernameAllowList(opCredentials, request.AllCredentials);
                logger.LogInformation("Passkey sign-in options for {Username}: offering {Offered} of {Approved} approved passkey(s)", request.Username, allowedCredentials.Count, opCredentials.Count(c => c.IsApproved && c.IsActive));
            }

            AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Required
            });

            string nonce = ceremonyCache.Store(options.ToJson());
            return Results.Ok(new { nonce, options = JsonSerializer.Deserialize<JsonElement>(options.ToJson()) });
        }

        /// <summary>
        /// True only for a request that reached Kestrel directly from this machine on localhost. Tunnel traffic also
        /// arrives from loopback (cloudflared connects to localhost), so a Cloudflare header or a public Host also disqualifies it.
        /// </summary>
        internal static bool IsDirectLocalRequest(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey("CF-Connecting-IP"))
            {
                return false;
            }

            // Local means this machine or the private network, reached by a local name or address. A tunnel request arrives from
            // loopback too, but with the public host name, so it is not local.
            return LocalNetwork.IsLocalHostName(context.Request.Host.Host)
                && context.Connection.RemoteIpAddress is { } remote && LocalNetwork.IsLocalAddress(remote);
        }

        private static async Task<IResult> UsernameAssertionCompleteAsync(
            ILogger<Program> logger,
            UsernameAssertionCompleteRequest request,
            IOperatorWriteRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            IWebAuthnCeremonyCache ceremonyCache,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            INotificationDispatcher? notificationDispatcher,
            HttpContext context,
            IFido2 fido2,
            ISecurityAuditService auditService,
            CancellationToken ct)
        {
            string? cachedOptionsJson = ceremonyCache.TryTake(request.Nonce);
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

            string credentialIdB64 = Convert.ToBase64String(assertionResponse.RawId);
            OperatorCredential? credential = await credentials.GetByWebAuthnCredentialIdAsync(credentialIdB64, ct);
            if (credential == null || credential.WebAuthnPublicKey == null)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    tierResolver.ResolveClientIp(context), "Unknown or revoked passkey", isUrgent: true, ct);

                // The passkey matched no operator. If the typed name matches no operator either, record it as an unknown
                // user so the security events page can show it. A known operator's revoked passkey is not an unknown user.
                if (!string.IsNullOrWhiteSpace(request.Username) && await operators.GetByUsernameAsync(request.Username, ct) is null)
                {
                    await auditService.RecordUnknownUserLoginAsync(request.Username, tierResolver.ResolveClientIp(context), "Unknown or revoked passkey", ct);
                }

                logger.LogWarning("Passkey sign-in rejected: the passkey returned is not an approved, active passkey of any operator");
                return Results.Unauthorized();
            }

            VerifyAssertionResult result;
            try
            {
                result = await fido2.MakeAssertionAsync(new MakeAssertionParams
                {
                    AssertionResponse = assertionResponse,
                    OriginalOptions = options,
                    StoredPublicKey = credential.WebAuthnPublicKey,
                    StoredSignatureCounter = credential.WebAuthnSignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = PasskeyUserHandleVerifier.ForOperator(credential.OperatorId)
                }, ct);
            }
            catch (Fido2VerificationException ex)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, credential.OperatorId, null,
                    tierResolver.ResolveClientIp(context), ex.Message, isUrgent: true, ct);
                logger.LogWarning("Passkey sign-in failed verification: {Reason}: {Message} ({ErrorType})", ex.Message, ex.Message, ex.GetType().Name);
                return Results.Unauthorized();
            }

            // Check operator is approved and active
            Operator? op = await operators.GetAsync(credential.OperatorId, ct);
            if (op == null || !op.IsApproved || !op.Active)
            {
                string reason = op == null ? "Operator not found" : !op.IsApproved ? "Your account is pending admin approval" : "Your account has been deactivated";
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, credential.OperatorId, null,
                    tierResolver.ResolveClientIp(context), reason, isUrgent: true, ct);
                return Results.Unauthorized();
            }

            // Record successful auth
            await credentials.RecordSuccessfulAuthAsync(credential.Id, result.SignCount, ct);

            // If a 2FA correlation ID is provided, validate it matches this operator
            if (!string.IsNullOrEmpty(request.TwoFactorCorrelationId))
            {
                Guid? correlatedOperatorId = twoFactorPendingAuthCache.TryTake(request.TwoFactorCorrelationId);
                if (correlatedOperatorId == null || correlatedOperatorId.Value != credential.OperatorId)
                {
                    // Invalid/expired correlation token or doesn't match this operator's password login
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, credential.OperatorId, null,
                        tierResolver.ResolveClientIp(context), "Invalid two-factor correlation token", isUrgent: true, ct);
                    return Results.Unauthorized();
                }
            }

            // First-login-after-credential-approval notification
            if (credential.FirstLoginNotifiedAtUtc == null)
            {
                try
                {
                    if (notificationDispatcher != null)
                    {
                        await notificationDispatcher.DispatchAsync(new NotificationEvent(
                            EventType: "OperatorCredentialFirstLogin",
                            TimestampUtc: DateTime.UtcNow,
                            OperatorId: op.Id,
                            PairedDeviceId: null,
                            SourceIp: tierResolver.ResolveClientIp(context),
                            Details: $"Operator '{op.DisplayName}' successfully authenticated with passkey '{credential.Label}' for the first time",
                            Audience: NotificationAudience.AdminsOnly,
                            Severity: NoticeSeverity.Info), ct);
                        await credentials.MarkFirstLoginNotifiedAsync(credential.Id, ct);
                    }
                }
                catch
                {
                    // Non-critical - log but don't fail auth if notification dispatch fails
                }
            }

            // First-login-after-approval notification for operator
            if (op.ApprovalFirstLoginNotifiedAtUtc == null)
            {
                try
                {
                    if (notificationDispatcher != null)
                    {
                        await notificationDispatcher.DispatchAsync(new NotificationEvent(
                            EventType: "OperatorApprovalFirstLogin",
                            TimestampUtc: DateTime.UtcNow,
                            OperatorId: op.Id,
                            PairedDeviceId: null,
                            SourceIp: tierResolver.ResolveClientIp(context),
                            Details: $"Approved operator '{op.DisplayName}' successfully logged in for the first time",
                            Audience: NotificationAudience.AdminsOnly,
                            Severity: NoticeSeverity.Info), ct);
                        await operators.SetApprovalFirstLoginNotifiedAsync(op.Id, ct);
                    }
                }
                catch
                {
                    // Non-critical - log but don't fail auth if notification dispatch fails
                }
            }

            string token = sessionTokens.Issue(op.Id, credential.Id, CredentialKind.OperatorPasskey, op.Role, op.SecurityStamp);

            await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null,
                tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);

            // A passkey was just verified for this operator, so hasPasskey is true by construction.
            return Results.Ok(new
            {
                sessionToken = token,
                operatorId = op.Id,
                role = op.Role.ToString(),
                mustChangePassword = op.MustChangePassword,
                hasPasskey = true
            });
        }

        private static async Task<IResult> CredentialRegisterOptionsAsync(
            CredentialRegisterOptionsRequest request,
            IOperatorWriteRepository operators,
            IWebAuthnCeremonyCache ceremonyCache,
            HttpContext context,
            IFido2 fido2,
            CancellationToken ct)
        {
            // Derive caller from claim
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
            {
                return Results.Unauthorized();
            }

            Operator? op = await operators.GetAsync(operatorId, ct);
            if (op == null)
            {
                return Results.Unauthorized();
            }

            CredentialCreateOptions options = fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    DisplayName = op.DisplayName,
                    Name = op.Username,
                    Id = op.Id.ToByteArray()
                },
                ExcludeCredentials = [],
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

            var pendingRegistration = new PendingCredentialRegistration(operatorId, request.Label, options.ToJson());
            string nonce = ceremonyCache.Store(JsonSerializer.Serialize(pendingRegistration));

            return Results.Ok(new { nonce, options = JsonSerializer.Deserialize<JsonElement>(options.ToJson()) });
        }

        private static async Task<IResult> CredentialRegisterCompleteAsync(
            CredentialRegisterCompleteRequest request,
            IOperatorWriteRepository operators,
            IOperatorCredentialRepository credentials,
            IWebAuthnCeremonyCache ceremonyCache,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            INotificationDispatcher? notificationDispatcher,
            HttpContext context,
            IFido2 fido2,
            CancellationToken ct)
        {
            // Derive caller from claim
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
            {
                return Results.Unauthorized();
            }

            string? cached = ceremonyCache.TryTake(request.Nonce);
            if (cached == null)
            {
                return Results.BadRequest(new { error = "Registration ceremony expired or already completed." });
            }

            PendingCredentialRegistration pending = JsonSerializer.Deserialize<PendingCredentialRegistration>(cached)!;

            // Verify caller is registering for their own account
            if (pending.OperatorId != operatorId)
            {
                return Results.Forbid();
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
                return Results.BadRequest(new { error = "Malformed attestation response." });
            }

            RegisteredPublicKeyCredential credential;
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

            Operator? op = await operators.GetAsync(operatorId, ct);
            if (op == null)
            {
                return Results.Unauthorized();
            }

            // New passkeys are UNAPPROVED unless the policy auto-approves (first passkey for the primary SuperAdmin or after a password reset)
            IReadOnlyList<OperatorCredential> existingCredentials = await credentials.ListForOperatorAsync(operatorId, ct);
            bool hasApprovedCredential = existingCredentials.Any(c => c.IsApproved && c.RevokedAtUtc == null);
            bool autoApprove = CredentialApprovalPolicy.ShouldAutoApprove(op, hasApprovedCredential);

            var newCredential = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                Label = pending.Label,
                WebAuthnCredentialId = Convert.ToBase64String(credential.Id),
                WebAuthnPublicKey = credential.PublicKey,
                WebAuthnSignCount = credential.SignCount,
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = autoApprove,
                RevokedAtUtc = null,
                FirstLoginNotifiedAtUtc = null
            };
            await credentials.AddAsync(newCredential, ct);

            // Notify admins of pending credential
            try
            {
                if (notificationDispatcher != null && !autoApprove)
                {
                    await notificationDispatcher.DispatchAsync(new NotificationEvent(
                        EventType: "OperatorCredentialPendingApproval",
                        TimestampUtc: DateTime.UtcNow,
                        OperatorId: operatorId,
                        PairedDeviceId: null,
                        SourceIp: tierResolver.ResolveClientIp(context),
                        Details: $"Operator '{op.DisplayName}' added a new passkey '{pending.Label}' awaiting admin approval",
                        Audience: NotificationAudience.AdminsOnly,
                        Severity: NoticeSeverity.Info), ct);
                }
            }
            catch
            {
                // Non-critical - log but don't fail if notification dispatch fails
            }

            string auditDetail = autoApprove
                ? $"Passkey auto-approved: {pending.Label}"
                : $"Pending credential added: {pending.Label}";
            await auditLog.LogAsync(SecurityAuditEventTypes.CredentialRegistered, operatorId, null,
                tierResolver.ResolveClientIp(context), auditDetail, isUrgent: false, ct);

            return Results.Ok(new { credentialId = newCredential.Id, isApproved = autoApprove });
        }
    }
}
