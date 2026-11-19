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
    /// <summary>
    /// Operator (human user) authentication endpoints for username-first passwordless and password-based login (plan Â§5.11/Â§5.12).
    ///
    /// These are DISTINCT from PairingEndpoints.cs which handles service/device pairing (QR + WebAuthn for MAUI/CLI).
    /// Here, we're implementing human operator self-service registration and login:
    /// - WebAuthn passkey registration and assertion (username-scoped, not device-scoped)
    /// - Password registration, login, and change
    /// - Step-up re-authentication via password (for operators without a passkey device)
    /// </summary>
    public static partial class OperatorAuthEndpoints
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
            IOperatorWriteRepository operators,
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
    }
}
