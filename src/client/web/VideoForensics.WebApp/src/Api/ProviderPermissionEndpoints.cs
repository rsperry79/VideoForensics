using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Reads and changes who may edit provider accounts. Reading is open to Admin and up so the UI can show the current
    /// setting. Changing it is SuperAdmin only, on the same passkey-or-local gate as the lockout policy beside it.
    /// </summary>
    public static class ProviderPermissionEndpoints
    {
        public static void MapProviderPermissionEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/permissions").RequireAuthorization(VideoForensicsPolicies.Admin);

            _ = group.MapGet("/provider-accounts", GetAsync)
                .WithName("GetProviderAccountPermissions");

            _ = group.MapPut("/provider-accounts", UpdateAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("UpdateProviderAccountPermissions");
        }

        private static async Task<IResult> GetAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(ProviderAccountEditPolicy.SettingKey, ct);
            return Results.Ok(new ProviderPermissionsDto(ProviderAccountEditPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> UpdateAsync(
            UpdateProviderPermissionsRequest request,
            IAppSettingRepository settings,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // Validated at the API boundary. Storing anything else would silently fall back to Admin on read.
            string level = request.ProviderAccountEditAccess;
            if (level != ProviderEditAccessLevels.Admin && level != ProviderEditAccessLevels.SuperAdmin)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.ProviderAccountEditAccess)] = ["Must be Admin or SuperAdmin."],
                });
            }

            await settings.SetAsync(ProviderAccountEditPolicy.SettingKey, level, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.ProviderPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"ProviderAccountEdit: {level}", isUrgent: true, ct);

            return Results.Ok(new ProviderPermissionsDto(level));
        }
    }
}
