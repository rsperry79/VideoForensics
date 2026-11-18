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
        private static async Task<IResult> RegisterAsync(
            RegisterRequest request,
            IOperatorWriteRepository operators,
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
            IOperatorWriteRepository operators,
            Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
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

            // Look up operator by username (may be null). Done first so every early rejection below can tell
            // whether the typed username matches an operator; only unmatched names are recorded as unknown users.
            Operator? op = await operators.GetByUsernameAsync(request.Username, ct);

            // Check: if password authentication is disabled, reject immediately (unless first-run setup is pending)
            bool isSetupPhase = await operators.IsEmptyAsync(ct);
            if (!isSetupPhase && !await authMethodsService.IsEnabledAsync("password", ct))
            {
                if (op == null)
                {
                    await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "Password sign-in disabled", ct);
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Password sign-in is disabled", isUrgent: true, ct);
                return new PasswordCheck(Results.Json(new { error = "Password sign-in is disabled." }, statusCode: StatusCodes.Status403Forbidden), null, sourceIpString);
            }

            // Check: if IP is in banned ranges, reject immediately.
            // This is deterministic (admin-curated), so always block without fail-open/fail-closed logic.
            if (sourceIp != null && await bannedIpService.IsIpBannedAsync(sourceIp, ct))
            {
                if (op == null)
                {
                    await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "Source IP banned", ct);
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

            // Check: if IP is blocked by threat intelligence, reject immediately.
            if (sourceIp != null && await threatIntelService.IsIpBlockedAsync(sourceIp, ct))
            {
                if (op == null)
                {
                    await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "Threat intel block", ct);
                }

                await auditLog.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                    sourceIpString, "Invalid username or password", isUrgent: true, ct);
                return new PasswordCheck(Results.Unauthorized(), null, sourceIpString);
            }

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
                        if (op == null)
                        {
                            await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "GeoIP lookup failed", ct);
                        }

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
                        if (op == null)
                        {
                            await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "Country blocked", ct);
                        }

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

                // No operator matches the username: record it as a security event with no operator and the typed name,
                // so the security-events page can show it. The password is never recorded.
                if (op == null)
                {
                    await auditService.RecordUnknownUserLoginAsync(request.Username, sourceIpString, "Invalid username", ct);
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
            IOperatorWriteRepository operators,
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
            Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
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

            // On the local network the operator's passkey setting decides: the password completes the sign-in, or leads to adding a passkey.
            LocalPasswordOutcome localOutcome = LocalLoginPolicy.Decide(op.PasskeyRequired, IsDirectLocalRequest(context));
            if (requiresTwoFactor && localOutcome == LocalPasswordOutcome.Complete)
            {
                requiresTwoFactor = false;
                await auditLog.LogAsync(SecurityAuditEventTypes.AuthSuccess, op.Id, null, sourceIpString,
                    "Passkey second factor skipped: local password sign-in allowed for this operator", isUrgent: true, ct);
            }

            if (requiresTwoFactor)
            {
                // Operator requires 2FA - check if they have at least one approved passkey
                IReadOnlyList<OperatorCredential> operatorCredentials = await credentials.ListForOperatorAsync(op.Id, ct);
                bool hasApprovedCredential = operatorCredentials.Any(c => c.IsApproved && !c.RevokedAtUtc.HasValue);

                if (!hasApprovedCredential || localOutcome == LocalPasswordOutcome.AddPasskey)
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
            IOperatorWriteRepository operators,
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
            Hosting.Contracts.IAuthMethodSettingsService authMethodsService,
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
            IOperatorWriteRepository operators,
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
    }
}
