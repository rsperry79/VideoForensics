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
        private static async Task<IResult> ChangePasswordAsync(
            ChangePasswordRequest request,
            IOperatorWriteRepository operators,
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
            IOperatorWriteRepository operators,
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
    }
}
