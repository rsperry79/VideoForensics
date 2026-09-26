using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Admin endpoints for managing account lockout policy settings: maximum failed login attempts,
    /// lockout duration, GeoIP-based country blocking, and lookup failure behavior.
    /// All routes require SuperAdmin+Local authorization (plan §5.10/§5.12).
    /// </summary>
    public static class LockoutPolicyEndpoints
    {
        public static void MapLockoutPolicyEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/lockout-policy").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", GetAsync).WithName("GetLockoutPolicy");
            _ = group.MapPut("/", UpdateAsync).WithName("UpdateLockoutPolicy").AddEndpointFilter<StepUpEndpointFilter>();
        }

        private static async Task<IResult> GetAsync(
            ILockoutPolicySettingsRepository repository,
            CancellationToken ct)
        {
            var settings = await repository.GetAsync(ct);
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
            var currentSettings = await repository.GetAsync(ct);

            var updatedSettings = new VideoForensics.Data.Common.Entities.LockoutPolicySettings
            {
                Id = currentSettings.Id,
                MaxFailedAttempts = request.MaxFailedAttempts,
                LockoutDurationMinutes = request.LockoutDurationMinutes,
                BlockedCountryCodes = request.BlockedCountryCodes,
                FailClosedOnLookupError = request.FailClosedOnLookupError,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId) is { Value: not null and not "" } claim
                    && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : null
            };

            await repository.UpsertAsync(updatedSettings, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            string details = $"MaxFailedAttempts: {request.MaxFailedAttempts}, LockoutDuration: {request.LockoutDurationMinutes}min, " +
                           $"BlockedCountries: {request.BlockedCountryCodes ?? "none"}, FailClosed: {request.FailClosedOnLookupError}";
            await auditLog.LogAsync(SecurityAuditEventTypes.LockoutPolicyUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);

            return Results.Ok(updatedSettings.ToDto());
        }
    }
}
