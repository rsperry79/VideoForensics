using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Admin endpoints for managing account lockout policy settings: maximum failed login attempts,
    /// lockout duration, GeoIP-based country blocking, and lookup failure behavior.
    /// All routes require SuperAdmin, Local for password sign-in or passkey from any tier (plan §5.10/§5.12).
    /// </summary>
    public static class LockoutPolicyEndpoints
    {
        public static void MapLockoutPolicyEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/lockout-policy").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", GetAsync).WithName("GetLockoutPolicy");
            _ = group.MapPut("/", UpdateAsync).WithName("UpdateLockoutPolicy").AddEndpointFilter<StepUpEndpointFilter>();
        }

        private static async Task<IResult> GetAsync(
            ILockoutPolicySettingsRepository repository,
            CancellationToken ct)
        {
            LockoutPolicySettings settings = await repository.GetAsync(ct);
            return Results.Ok(settings.ToDto());
        }

        private static async Task<IResult> UpdateAsync(
            UpdateLockoutPolicySettingsRequest request,
            ILockoutPolicySettingsRepository repository,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            Dictionary<string, string[]> validationErrors = ValidateAutoBanFields(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            LockoutPolicySettings currentSettings = await repository.GetAsync(ct);

            var updatedSettings = new LockoutPolicySettings
            {
                Id = currentSettings.Id,
                MaxFailedAttempts = request.MaxFailedAttempts,
                LockoutDurationMinutes = request.LockoutDurationMinutes,
                BlockedCountryCodes = request.BlockedCountryCodes,
                FailClosedOnLookupError = request.FailClosedOnLookupError,
                AutoBanEnabled = request.AutoBanEnabled,
                AutoBanDistinctAccountThreshold = request.AutoBanDistinctAccountThreshold,
                AutoBanWindowMinutes = request.AutoBanWindowMinutes,
                AutoBanDurationMinutes = request.AutoBanDurationMinutes,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId) is { Value: not null and not "" } claim
                    && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : null
            };

            await repository.UpsertAsync(updatedSettings, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            string details = $"MaxFailedAttempts: {request.MaxFailedAttempts}, LockoutDuration: {request.LockoutDurationMinutes}min, " +
                           $"BlockedCountries: {request.BlockedCountryCodes ?? "none"}, FailClosed: {request.FailClosedOnLookupError}, " +
                           $"AutoBan: {request.AutoBanEnabled} ({request.AutoBanDistinctAccountThreshold} accounts/{request.AutoBanWindowMinutes}min, banned {request.AutoBanDurationMinutes}min)";
            await auditLog.LogAsync(SecurityAuditEventTypes.LockoutPolicyUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);

            return Results.Ok(updatedSettings.ToDto());
        }

        /// <summary>
        /// Rejects auto-ban counts and durations below 1. Validated here at the API boundary (CLAUDE.md input validation rule)
        /// because a zero threshold or window would make the tracker ban on every failure or never expire a ban.
        /// </summary>
        private static Dictionary<string, string[]> ValidateAutoBanFields(UpdateLockoutPolicySettingsRequest request)
        {
            var errors = new Dictionary<string, string[]>();
            if (request.AutoBanDistinctAccountThreshold < 1)
            {
                errors[nameof(request.AutoBanDistinctAccountThreshold)] = ["Must be at least 1."];
            }

            if (request.AutoBanWindowMinutes < 1)
            {
                errors[nameof(request.AutoBanWindowMinutes)] = ["Must be at least 1."];
            }

            if (request.AutoBanDurationMinutes < 1)
            {
                errors[nameof(request.AutoBanDurationMinutes)] = ["Must be at least 1."];
            }

            return errors;
        }
    }
}
