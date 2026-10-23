using Fido2NetLib;
using Fido2NetLib.Objects;

using Microsoft.AspNetCore.Identity;

using System.Net;
using System.Security.Claims;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Operator (human user) authentication endpoints for username-first passwordless and password-based login (plan Â§5.11/Â§5.12).
    ///
    /// These are DISTINCT from PairingEndpoints.cs which handles service/device pairing (QR + WebAuthn for MAUI/CLI).
    /// Here, we're implementing human operator self-service registration and login:
    /// - WebAuthn passkey registration and assertion (username-scoped, not device-scoped)
    /// - Password registration, login, and change
    /// - Step-up re-authentication via password (for operators without a passkey device)
    /// </summary>
    public static class OperatorAuthEndpoints
    {
        /// <summary>
        /// Dummy operator used for consistent password verification timing (timing-attack mitigation).
        /// Fields are minimal since we only use this for password hash verification, never for real operations.
        /// </summary>
        private static readonly Operator DummyOperator = new()
        {
            Id = Guid.Empty,
            DisplayName = "Dummy",
            Username = "dummy",
            FirstName = "Dummy",
            LastName = "User",
            Email = "dummy@example.invalid",
            Role = OperatorRole.ReadOnly,
            CreatedAtUtc = DateTime.UtcNow,
            SecurityStamp = Guid.Empty
        };

        /// <summary>
        /// Dummy password hash used for username-enumeration timing-attack mitigation.
        /// This is a known PBKDF2 hash of a random string, so password verification against it
        /// takes the same CPU time as verification against a real operator's hash, preventing
        /// attackers from discovering valid usernames by timing the login endpoint.
        /// </summary>
        private static readonly string DummyPasswordHash = new PasswordHasher<Operator>()
            .HashPassword(DummyOperator, "DummyPasswordForTimingAttackMitigation123!");

        public static void MapOperatorAuthEndpoints(this WebApplication app)
        {
            // Username-first WebAuthn assertion (sign-in by username, then prove passkey)
            _ = app.MapPost("/api/v1/auth/webauthn/username-assertion-options", UsernameAssertionOptionsAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Get WebAuthn assertion options for username-based passkey sign-in");

            _ = app.MapPost("/api/v1/auth/webauthn/username-assertion-complete", UsernameAssertionCompleteAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Complete WebAuthn passkey sign-in assertion");

            // Operator credential (additional passkey) registration - AUTHENTICATED
            _ = app.MapPost("/api/v1/auth/operator-credentials/register/options", CredentialRegisterOptionsAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .RequireRateLimiting("auth")
                .WithSummary("Get WebAuthn credential creation options");

            _ = app.MapPost("/api/v1/auth/operator-credentials/register/complete", CredentialRegisterCompleteAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .RequireRateLimiting("auth")
                .WithSummary("Complete WebAuthn passkey registration");

            // Self-service registration and password login
            _ = app.MapPost("/api/v1/auth/register", RegisterAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Register a new operator account (username + password)");

            _ = app.MapPost("/api/v1/auth/login/password", LoginPasswordAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Login with operator username and password")
                .Produces<dynamic>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            // Password sign-in that always issues a LIMITED passkey-setup session (15 min), used to add a passkey.
            _ = app.MapPost("/api/v1/auth/login/password/create-passkey", LoginPasswordCreatePasskeyAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Verify password and issue a limited session to register a passkey")
                .Produces<dynamic>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            // Password management - AUTHENTICATED
            _ = app.MapPost("/api/v1/auth/change-password", ChangePasswordAsync)
                .RequireAuthorization()
                .RequireRateLimiting("auth")
                .WithSummary("Change the caller's password");

            // Step-up re-authentication via password - AUTHENTICATED
            _ = app.MapPost("/api/v1/auth/stepup/password", StepUpPasswordAsync)
                .RequireAuthorization()
                .RequireRateLimiting("auth")
                .WithSummary("Issue a step-up token by re-verifying password");

            app.MapSessionKindEndpoint();
        }

        /// <summary>
        /// GET /api/v1/auth/session-kind - AUTHENTICATED. Reports whether the caller's session is the limited
        /// passkey-setup session. The answer comes from the claim the auth handler set from the validated token.
        /// </summary>
        internal static void MapSessionKindEndpoint(this WebApplication app)
        {
            _ = app.MapGet("/api/v1/auth/session-kind", GetSessionKindAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .WithSummary("Report whether the current session is limited to passkey setup");
        }

        private static async Task<IResult> GetSessionKindAsync(
            ClaimsPrincipal user,
            IOperatorRepository operators,
            ITwoFactorRoleRequirementRepository roleRequirements,
            CancellationToken ct)
        {
            // The UI reads these to hide Local-only and passkey-only pages; the policies remain the enforcement.
            string? tierClaim = user.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;
            bool localTier = tierClaim != null && Enum.TryParse<NetworkTier>(tierClaim, out NetworkTier tier) && tier == NetworkTier.Local;
            string? credentialKind = user.FindFirst(VideoForensicsClaimTypes.CredentialKind)?.Value;
            bool passkeySession = credentialKind == "OperatorPasskey";
            // Password sessions re-enter their password for step-up; the client needs this to pick the prompt.
            bool passwordSession = credentialKind == "Password";
            // The role's policy requires a second factor (a passkey). A password-only operator must upgrade before a
            // destructive change instead of confirming with a password.
            bool twoFactorRequired = false;
            if (Guid.TryParse(user.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value, out Guid operatorId))
            {
                Operator? op = await operators.GetAsync(operatorId, ct);
                if (op is not null)
                {
                    twoFactorRequired = await TwoFactorPolicyResolver.ResolveTwoFactorRequirementAsync(op, roleRequirements, ct);
                }
            }

            return Results.Ok(new
            {
                passkeySetupOnly = user.HasClaim(VideoForensicsClaimTypes.PasskeySetupOnly, "true"),
                localTier,
                passkeySession,
                passwordSession,
                twoFactorRequired,
            });
        }
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
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            IWebAuthnCeremonyCache ceremonyCache,
            ILogger<Program> logger,
            IFido2 fido2,
            CancellationToken ct)
        {
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

            string host = context.Request.Host.Host;
            bool localHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("127.0.0.1", StringComparison.Ordinal);
            return localHost && context.Connection.RemoteIpAddress is { } remote && System.Net.IPAddress.IsLoopback(remote);
        }

        private static async Task<IResult> UsernameAssertionCompleteAsync(
            ILogger<Program> logger,
            UsernameAssertionCompleteRequest request,
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            IWebAuthnCeremonyCache ceremonyCache,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            INotificationDispatcher? notificationDispatcher,
            HttpContext context,
            IFido2 fido2,
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
                logger.LogWarning(ex, "Passkey sign-in failed verification: {Reason}", ex.Message);
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
            IOperatorRepository operators,
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
            IOperatorRepository operators,
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

        private static async Task<IResult> RegisterAsync(
            RegisterRequest request,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            INotificationDispatcher? notificationDispatcher,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate password length
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 12)
            {
                return Results.BadRequest(new { error = "Password must be at least 12 characters." });
            }

            // Check username not already taken
            Operator? existingByUsername = await operators.GetByUsernameAsync(request.Username, ct);
            if (existingByUsername != null)
            {
                return Results.Conflict(new { error = "Username already in use." });
            }

            // Check email not already taken
            IReadOnlyList<Operator> allOps = await operators.ListAsync(ct);
            if (allOps.Any(o => o.Email == request.Email))
            {
                return Results.Conflict(new { error = "Email already in use." });
            }

            var passwordHasher = new PasswordHasher<Operator>();

            var newOperator = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = request.DisplayName ?? request.FirstName,
                Username = request.Username,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Phone = request.Phone,
                Role = OperatorRole.ReadOnly,
                IsApproved = false,
                Active = true,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid(),
                PasswordUpdatedAtUtc = DateTime.UtcNow,
                MustChangePassword = false
            };

            // Hash password
            newOperator.PasswordHash = passwordHasher.HashPassword(newOperator, request.Password);

            Operator created = await operators.AddAsync(newOperator, ct);

            // Notify admins of pending approval
            try
            {
                if (notificationDispatcher != null)
                {
                    await notificationDispatcher.DispatchAsync(new NotificationEvent(
                        EventType: "NewOperatorPendingApproval",
                        TimestampUtc: DateTime.UtcNow,
                        OperatorId: created.Id,
                        PairedDeviceId: null,
                        SourceIp: tierResolver.ResolveClientIp(context),
                        Details: $"New operator '{created.DisplayName}' ({created.Email}) registered and awaits admin approval",
                        Audience: NotificationAudience.AdminsOnly,
                        Severity: NoticeSeverity.Warning), ct);
                }
            }
            catch
            {
                // Non-critical - log but don't fail registration if notification dispatch fails
            }

            await auditLog.LogAsync(SecurityAuditEventTypes.OperatorRegistered, created.Id, null,
                tierResolver.ResolveClientIp(context), "New operator registered and awaits admin approval", isUrgent: false, ct);

            return Results.Ok(new { operatorId = created.Id, isApproved = created.IsApproved });
        }

        /// <summary>Outcome of the password checks shared by every password sign-in endpoint. Failure is set exactly when Operator is null.</summary>
        private sealed record PasswordCheck(IResult? Failure, Operator? Operator, string SourceIp);

        /// <summary>
        /// Runs every check a password sign-in must pass before any session is issued: password sign-in enabled, banned and
        /// threat-intel IPs, primary SuperAdmin local-only, lockout, blocked countries, password verification (failure counting
        /// and lockout audit), then approval and active status. /login/password and /login/password/create-passkey both call this,
        /// so the two endpoints cannot drift apart. Nothing here may be skipped by either caller.
        /// </summary>
        private static async Task<PasswordCheck> VerifyPasswordAsync(
            LoginPasswordRequest request,
            IOperatorRepository operators,
            VideoForensics.Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILockoutPolicySettingsRepository lockoutPolicy,
            IBannedIpMatchService bannedIpService,
            IThreatIntelBlocklistService threatIntelService,
            IGeoIpLookupService geoIpService,
            ISecurityAuditService auditService,
            IFailedLoginIpTracker failedLoginIpTracker,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            // Resolve the caller's source IP address early for all subsequent checks
            string sourceIpString = tierResolver.ResolveClientIp(context);
            IPAddress? sourceIp = null;
            if (IPAddress.TryParse(sourceIpString, out IPAddress? parsedIp))
            {
                sourceIp = parsedIp;
            }

            // Check: if password authentication is disabled, reject immediately (unless first-run setup is pending)
            bool isSetupPhase = await operators.IsEmptyAsync(ct);
            if (!isSetupPhase && !await authMethodsService.IsEnabledAsync("password", ct))
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Password sign-in is disabled", isUrgent: true, ct);
                return new PasswordCheck(Results.Json(new { error = "Password sign-in is disabled." }, statusCode: StatusCodes.Status403Forbidden), null, sourceIpString);
            }

            // Check: if IP is in banned ranges, reject immediately.
            // This is deterministic (admin-curated), so always block without fail-open/fail-closed logic.
            if (sourceIp != null && await bannedIpService.IsIpBannedAsync(sourceIp, ct))
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // Check: if IP is blocked by threat intelligence, reject immediately.
            if (sourceIp != null && await threatIntelService.IsIpBlockedAsync(sourceIp, ct))
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // Look up operator by username (may be null)
            Operator? op = await operators.GetByUsernameAsync(request.Username, ct);

            // Check: if operator is primary SuperAdmin, they may only log in from Local tier.
            // We do this check BEFORE password verification to avoid timing leaks on which accounts are primary.
            // If the operator exists but is not local-tier, reject with generic error (don't reveal the reason).
            //
            // This is itself a PRE-AUTH self-call target when reached through the WebApp's own
            // DeviceSignIn.razor page (WebAuthnClient.SignInWithPasswordAsync posts here with no
            // session token yet) - that self-call's own connection is always loopback regardless of
            // where the real browser is, so RequestTierResolver.ResolvePreAuth (not a bare
            // tierResolver.ResolveTier(context)) recovers the real tier from the pre-auth
            // X-VF-Session-Tier header the same way an already-authenticated self-call's is.
            if (op != null && op.IsPrimarySuperAdmin)
            {
                NetworkTier currentTier = RequestTierResolver.ResolvePreAuth(context, tierResolver, headerProtector, logger);
                if (currentTier != NetworkTier.Local)
                {
                    // Primary SuperAdmin attempting login from non-local tier - reject with generic failure.
                    // Don't perform password verification (timing is less critical since we're rejecting anyway).
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                        sourceIpString, "Invalid username or password", isUrgent: true, ct);
                    return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
                }
            }

            // Check: if operator is locked out (LockedOutUntilUtc is in the future), reject immediately.
            // This check happens BEFORE password verification to prevent timing leaks.
            if (op != null && op.LockedOutUntilUtc.HasValue && op.LockedOutUntilUtc.Value > DateTime.UtcNow)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, op.Id, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // Check: if BlockedCountryCodes is configured, check if this IP's country is blocked.
            LockoutPolicySettings policy = await lockoutPolicy.GetAsync(ct);
            if (!string.IsNullOrEmpty(policy.BlockedCountryCodes) && sourceIp != null)
            {
                string? countryCode = await geoIpService.LookupCountryCodeAsync(sourceIp, ct);
                if (countryCode == null)
                {
                    // GeoIP lookup failed. Consult FailClosedOnLookupError to decide behavior.
                    if (policy.FailClosedOnLookupError)
                    {
                        // Fail closed: treat as blocked
                        await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                            sourceIpString, "Invalid username or password", isUrgent: true, ct);
                        return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
                    }
                    // Otherwise, fail open: continue to password verification
                }
                else
                {
                    // Lookup succeeded - check if this country is in the blocked list
                    var blockedCountries = policy.BlockedCountryCodes.Split(',').Select(c => c.Trim()).ToList();
                    if (blockedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
                    {
                        // Country is blocked
                        await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                            sourceIpString, "Invalid username or password", isUrgent: true, ct);
                        return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
                    }
                }
            }

            // Prepare password verification: if operator exists, use their hash; otherwise use dummy hash.
            // This ensures consistent verification time regardless of whether the username exists,
            // preventing username enumeration via timing attack.
            var passwordHasher = new PasswordHasher<Operator>();
            string hashToVerify = op?.PasswordHash ?? DummyPasswordHash;

            // Use actual operator for verification if they exist, otherwise use the pre-built DummyOperator
            Operator operatorForVerification = op ?? DummyOperator;
            PasswordVerificationResult verificationResult = passwordHasher.VerifyHashedPassword(operatorForVerification, hashToVerify, request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                // Password verification failed. If operator exists, increment failed login attempts and check for lockout.
                if (op != null)
                {
                    // Audit log for invalid password attempt
                    await auditService.RecordLoginAttemptAsync(op.Id, sourceIpString, success: false, reason: "Invalid password", ct);

                    await operators.IncrementFailedLoginAttemptAsync(
                        op.Id, policy.MaxFailedAttempts, policy.LockoutDurationMinutes, ct);

                    // Detect and audit account lockout if triggered
                    if (op.LockedOutUntilUtc.HasValue && op.LockedOutUntilUtc.Value > DateTime.UtcNow)
                    {
                        await auditService.RecordAccountLockoutAsync(op.Id, sourceIpString, ct);
                        // Result deliberately ignored: a ban (or not) must not change the generic 401 this request returns.
                        _ = await failedLoginIpTracker.RecordLockoutAsync(sourceIpString, op.Id, policy, ct);
                    }
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, op?.Id, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // If we get here, the password is correct. But if op is null, we shouldn't proceed.
            if (op == null)
            {
                // This shouldn't happen (we verified against dummy hash and it passed, which is extremely unlikely).
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // Password verification succeeded - reset failed login attempts for this operator.
            await operators.ResetFailedLoginAttemptsAsync(op.Id, ct);

            // Audit log for successful password verification
            await auditService.RecordLoginAttemptAsync(op.Id, sourceIpString, success: true, reason: null, ct);

            // Check approval and active status
            if (!op.IsApproved)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, op.Id, null,
                    sourceIpString, "Account pending admin approval", isUrgent: true, ct);
                return new PasswordCheck(Results.Json(new { error = "Your account is pending admin approval. Contact an administrator." }, statusCode: StatusCodes.Status401Unauthorized), null, sourceIpString);
            }

            if (!op.Active)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, op.Id, null,
                    sourceIpString, "Account deactivated", isUrgent: true, ct);
                return new PasswordCheck(Results.Json(new { error = "Your account has been deactivated. Contact an administrator." }, statusCode: StatusCodes.Status401Unauthorized), null, sourceIpString);
            }

            return new PasswordCheck(null, op, sourceIpString);
        }

        private static async Task<IResult> LoginPasswordAsync(
            LoginPasswordRequest request,
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILockoutPolicySettingsRepository lockoutPolicy,
            ITwoFactorRoleRequirementRepository twoFactorRequirements,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            INotificationDispatcher? notificationDispatcher,
            IBannedIpMatchService bannedIpService,
            IThreatIntelBlocklistService threatIntelService,
            IGeoIpLookupService geoIpService,
            ISecurityAuditService auditService,
            VideoForensics.Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
            IFailedLoginIpTracker failedLoginIpTracker,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            // Shared password checks (VerifyPasswordAsync). Nothing below runs unless they all pass.
            PasswordCheck check = await VerifyPasswordAsync(request, operators, authMethodsService, auditLog, tierResolver, headerProtector,
                lockoutPolicy, bannedIpService, threatIntelService, geoIpService, auditService, failedLoginIpTracker, context, logger, ct);
            if (check.Operator == null)
            {
                return check.Failure!;
            }

            Operator op = check.Operator;
            string sourceIpString = check.SourceIp;

            // Check two-factor authentication requirement
            bool requiresTwoFactor = await TwoFactorPolicyResolver.ResolveTwoFactorRequirementAsync(op, twoFactorRequirements, ct);

            // Windows Hello disabled on this machine: a local password sign-in may complete without the passkey second factor.
            if (requiresTwoFactor && !request.PasskeyAvailable && IsDirectLocalRequest(context))
            {
                requiresTwoFactor = false;
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null, sourceIpString,
                    "Passkey second factor skipped: local password sign-in with Windows Hello disabled", isUrgent: true, ct);
            }

            if (requiresTwoFactor)
            {
                // Operator requires 2FA - check if they have at least one approved passkey
                IReadOnlyList<OperatorCredential> operatorCredentials = await credentials.ListForOperatorAsync(op.Id, ct);
                bool hasApprovedCredential = operatorCredentials.Any(c => c.IsApproved && !c.RevokedAtUtc.HasValue);

                if (!hasApprovedCredential)
                {
                    // Operator with 2FA required but no approved passkey yet: they cannot complete a second factor,
                    // so issue a LIMITED session that only the passkey setup endpoints accept
                    // (see PasskeySetupSessionPolicy). A full session here would let an unenrolled operator bypass 2FA.
                    string token = sessionTokens.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp);

                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null,
                        sourceIpString, null, isUrgent: false, ct);

                    await NotifyFirstLoginAfterApprovalAsync(op, operators, notificationDispatcher, sourceIpString, ct);

                    return Results.Ok(new
                    {
                        sessionToken = token,
                        operatorId = op.Id,
                        role = op.Role.ToString(),
                        mustChangePassword = op.MustChangePassword,
                        requiresPasskeyRegistration = true,
                        hasPasskey = operatorCredentials.Any(c => !c.RevokedAtUtc.HasValue)
                    });
                }
                else
                {
                    // Operator has approved credentials - require 2FA step-2 completion
                    string correlationToken = twoFactorPendingAuthCache.Store(op.Id);
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null,
                        sourceIpString, "Password verified, pending two-factor completion", isUrgent: false, ct);

                    return Results.Ok(new
                    {
                        requiresTwoFactor = true,
                        twoFactorCorrelationId = correlationToken,
                        // Second-factor methods this server accepts; clients render only the ones they know.
                        twoFactorMethods = new[] { "passkey" }
                    });
                }
            }

            // 2FA not required - proceed with normal login.
            // hasPasskey tells the UI whether to send the operator to passkey setup. Only OperatorCredential
            // rows count; PairedDevice records are device-level and must not satisfy the check.
            IReadOnlyList<OperatorCredential> accountCredentials = await credentials.ListForOperatorAsync(op.Id, ct);
            bool hasPasskey = accountCredentials.Any(c => !c.RevokedAtUtc.HasValue);
            string sessionToken = sessionTokens.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp);

            await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null,
                sourceIpString, null, isUrgent: false, ct);

            await NotifyFirstLoginAfterApprovalAsync(op, operators, notificationDispatcher, sourceIpString, ct);

            return Results.Ok(new
            {
                sessionToken,
                operatorId = op.Id,
                role = op.Role.ToString(),
                mustChangePassword = op.MustChangePassword,
                hasPasskey
            });
        }

        /// <summary>
        /// POST /api/v1/auth/login/password/create-passkey. Same body and same checks as /login/password (via VerifyPasswordAsync),
        /// but always issues a LIMITED passkey-setup session (15 minutes, passkey setup endpoints only), even when the operator
        /// already has passkeys. The 2FA settings and credential state are deliberately not consulted here: this path never
        /// completes a second factor. The parameter list matches /login/password so both bind the same services.
        /// </summary>
        private static async Task<IResult> LoginPasswordCreatePasskeyAsync(
            LoginPasswordRequest request,
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILockoutPolicySettingsRepository lockoutPolicy,
            ITwoFactorRoleRequirementRepository twoFactorRequirements,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            INotificationDispatcher? notificationDispatcher,
            IBannedIpMatchService bannedIpService,
            IThreatIntelBlocklistService threatIntelService,
            IGeoIpLookupService geoIpService,
            ISecurityAuditService auditService,
            VideoForensics.Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
            IFailedLoginIpTracker failedLoginIpTracker,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            PasswordCheck check = await VerifyPasswordAsync(request, operators, authMethodsService, auditLog, tierResolver, headerProtector,
                lockoutPolicy, bannedIpService, threatIntelService, geoIpService, auditService, failedLoginIpTracker, context, logger, ct);
            if (check.Operator == null)
            {
                return check.Failure!;
            }

            Operator op = check.Operator;
            IReadOnlyList<OperatorCredential> operatorCredentials = await credentials.ListForOperatorAsync(op.Id, ct);
            string token = sessionTokens.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp);

            await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null,
                check.SourceIp, "Passkey setup session issued", isUrgent: false, ct);

            await NotifyFirstLoginAfterApprovalAsync(op, operators, notificationDispatcher, check.SourceIp, ct);

            return Results.Ok(new
            {
                sessionToken = token,
                operatorId = op.Id,
                role = op.Role.ToString(),
                mustChangePassword = op.MustChangePassword,
                requiresPasskeyRegistration = true,
                hasPasskey = operatorCredentials.Any(c => !c.RevokedAtUtc.HasValue)
            });
        }

        /// <summary>Sends the one-time "approved operator logged in for the first time" admin notice. A failure here never fails the sign-in.</summary>
        private static async Task NotifyFirstLoginAfterApprovalAsync(
            Operator op,
            IOperatorRepository operators,
            INotificationDispatcher? notificationDispatcher,
            string sourceIpString,
            CancellationToken ct)
        {
            if (op.ApprovalFirstLoginNotifiedAtUtc != null)
            {
                return;
            }

            try
            {
                if (notificationDispatcher != null)
                {
                    await notificationDispatcher.DispatchAsync(new NotificationEvent(
                        EventType: "OperatorApprovalFirstLogin",
                        TimestampUtc: DateTime.UtcNow,
                        OperatorId: op.Id,
                        PairedDeviceId: null,
                        SourceIp: sourceIpString,
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

        private static async Task<IResult> ChangePasswordAsync(
            ChangePasswordRequest request,
            IOperatorRepository operators,
            ISessionTokenService sessionTokens,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
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

            // Validate new password length
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 12)
            {
                return Results.BadRequest(new { error = "Password must be at least 12 characters." });
            }

            var passwordHasher = new PasswordHasher<Operator>();

            // If NOT forced to change, verify current password
            if (!op.MustChangePassword)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                {
                    return Results.BadRequest(new { error = "Current password required." });
                }

                if (op.PasswordHash == null)
                {
                    // No password set yet on this account - nothing to verify against.
                    return Results.BadRequest(new { error = "No password is set on this account." });
                }

                PasswordVerificationResult verificationResult = passwordHasher.VerifyHashedPassword(op, op.PasswordHash, request.CurrentPassword);
                if (verificationResult == PasswordVerificationResult.Failed)
                {
                    await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, op.Id, null,
                        tierResolver.ResolveClientIp(context), "Invalid current password", isUrgent: true, ct);
                    return Results.Json(new { error = "Current password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
                }
            }

            // Hash new password and update
            string newHash = passwordHasher.HashPassword(op, request.NewPassword);
            await operators.SetPasswordAsync(op.Id, newHash, mustChangePassword: false, ct);

            // Reload to get updated SecurityStamp
            op = await operators.GetAsync(op.Id, ct);
            if (op == null)
            {
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            // Issue fresh session so user isn't logged out after password change
            string freshToken = sessionTokens.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp);

            await auditLog.LogAsync(SecurityAuditEventTypes.PasswordReset, op.Id, null,
                tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);

            return Results.Ok(new { sessionToken = freshToken });
        }

        private static async Task<IResult> StepUpPasswordAsync(
            StepUpPasswordRequest request,
            IOperatorRepository operators,
            ITwoFactorRoleRequirementRepository roleRequirements,
            IStepUpAuthService stepUpAuth,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // Derive caller from claim
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
            {
                return Results.Unauthorized();
            }

            Operator? op = await operators.GetAsync(operatorId, ct);
            if (op == null || op.PasswordHash == null)
            {
                // No password set on this account (e.g. passkey-only or a service-account operator) -
                // this step-up path isn't usable for them; fail rather than crash on a null hash.
                return Results.Unauthorized();
            }

            // Policy: a role whose two-factor requirement is a passkey cannot confirm with a password. Refused before the
            // password is checked, so a password-only operator is told to upgrade rather than tested.
            if (await TwoFactorPolicyResolver.ResolveTwoFactorRequirementAsync(op, roleRequirements, ct))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Verify password
            var passwordHasher = new PasswordHasher<Operator>();
            PasswordVerificationResult verificationResult = passwordHasher.VerifyHashedPassword(op, op.PasswordHash, request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                await auditLog.LogAsync(SecurityAuditEventTypes.StepUpFailed, op.Id, null,
                    tierResolver.ResolveClientIp(context), "Invalid password", isUrgent: true, ct);
                return Results.Unauthorized();
            }

            // Issue step-up token
            string stepUpToken = stepUpAuth.IssueToken(operatorId);

            await auditLog.LogAsync(SecurityAuditEventTypes.StepUpVerified, op.Id, null,
                tierResolver.ResolveClientIp(context), null, isUrgent: false, ct);

            return Results.Ok(new { stepUpToken });
        }

        private static async Task<IResult> RecoverPasswordAsync(
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            HttpContext context,
            CancellationToken ct)
        {
            // Check if request is from localhost only
            if (context.Connection.RemoteIpAddress != IPAddress.Loopback && context.Connection.RemoteIpAddress != IPAddress.IPv6Loopback)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Check if any operators exist
            IReadOnlyList<Operator> allOps = await operators.ListAsync(ct);
            if (allOps.Count == 0)
            {
                return Results.StatusCode(StatusCodes.Status404NotFound);
            }

            // Get the primary SuperAdmin operator
            Operator? primaryAdmin = allOps.FirstOrDefault(o => o.IsPrimarySuperAdmin);
            // Fallback to any SuperAdmin if primary doesn't exist
            primaryAdmin ??= allOps.FirstOrDefault(o => o.Role == OperatorRole.SuperAdmin);

            if (primaryAdmin == null)
            {
                // No SuperAdmin found at all
                return Results.StatusCode(StatusCodes.Status404NotFound);
            }

            // Generate temporary password (16-char alphanumeric) using cryptographically secure RNG
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            byte[] randomBytes = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
            string temporaryPassword = string.Concat(randomBytes.Select(b => chars[b % chars.Length]));

            // Hash the password
            var passwordHasher = new PasswordHasher<Operator>();
            string passwordHash = passwordHasher.HashPassword(primaryAdmin, temporaryPassword);

            // Update operator with new password and set MustChangePassword flag
            await operators.SetPasswordAsync(primaryAdmin.Id, passwordHash, mustChangePassword: true, ct);

            // Log the recovery event
            await auditLog.LogAsync(
                SecurityAuditEventTypes.PasswordReset,
                primaryAdmin.Id,
                null,
                "127.0.0.1",
                "Localhost password recovery requested",
                isUrgent: true,
                ct);

            // Return temporary password (only time it's shown in plaintext)
            return Results.Ok(new { temporaryPassword });
        }
    }

    // Request/response DTOs
    /// <param name="Username">The operator signing in.</param>
    /// <param name="AllCredentials">False offers only the most recently used passkey; true offers every approved passkey (the fallback).</param>
    public record UsernameAssertionOptionsRequest(string Username, bool AllCredentials = false);
    public record UsernameAssertionCompleteRequest(string Nonce, JsonElement AssertionResponse, string? TwoFactorCorrelationId = null);
    public record CredentialRegisterOptionsRequest(string Label);
    public record CredentialRegisterCompleteRequest(string Nonce, JsonElement AttestationResponse, string Label);
    public record RegisterRequest(string Username, string Password, string FirstName, string LastName, string Email, string? Phone = null, string? DisplayName = null);
    /// <param name="PasskeyAvailable">False when this device cannot run a passkey ceremony (Windows Hello disabled). Only honored for a direct local request.</param>
    public record LoginPasswordRequest(string Username, string Password, bool PasskeyAvailable = true);
    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
    public record StepUpPasswordRequest(string Password);

    internal record PendingCredentialRegistration(Guid OperatorId, string Label, string OptionsJson);
}

