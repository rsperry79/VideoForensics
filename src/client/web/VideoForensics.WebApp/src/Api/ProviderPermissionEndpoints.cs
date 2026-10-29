using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Reads and changes who may edit provider accounts and who may open the restricted settings pages (reports, email, notifications, LLM API). Reading is open to Admin and up so the UI can show the current
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

            _ = group.MapGet("/settings-access", GetSettingsAccessAsync)
                .WithName("GetSettingsAccess");

            // One read and one write per restricted settings page. Changing one is SuperAdmin only, on the same
            // passkey-or-local gate and step-up as the other permission settings.
            foreach ((string route, RestrictedSettings area) in Areas)
            {
                _ = group.MapGet($"/{route}", (IAppSettingRepository settings, CancellationToken ct) => GetAreaAsync(area, settings, ct))
                    .WithName($"Get{area}Permissions");

                _ = group.MapPut($"/{route}", (UpdateSettingsAccessRequest request, IAppSettingRepository settings, ISecurityAuditLogger auditLog, INetworkTierResolver tierResolver, HttpContext context, CancellationToken ct) =>
                        UpdateAreaAsync(area, request, settings, auditLog, tierResolver, context, ct))
                    .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                    .AddEndpointFilter<StepUpEndpointFilter>()
                    .WithName($"Update{area}Permissions");
            }
        }

        private static readonly (string Route, RestrictedSettings Area)[] Areas =
        [
            ("reports", RestrictedSettings.Reports),
            ("email", RestrictedSettings.Email),
            ("notifications", RestrictedSettings.Notifications),
            ("llm", RestrictedSettings.Llm),
        ];

        private static async Task<IResult> GetSettingsAccessAsync(ISettingsAccessPolicy policy, CancellationToken ct)
        {
            return Results.Ok(new SettingsAccessDto(
                await policy.GetRequiredPolicyAsync(RestrictedSettings.Reports, ct) == AccessPolicy.Admin,
                await policy.GetRequiredPolicyAsync(RestrictedSettings.Email, ct) == AccessPolicy.Admin,
                await policy.GetRequiredPolicyAsync(RestrictedSettings.Notifications, ct) == AccessPolicy.Admin,
                await policy.GetRequiredPolicyAsync(RestrictedSettings.Llm, ct) == AccessPolicy.Admin));
        }

        private static async Task<IResult> GetAreaAsync(RestrictedSettings area, IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(SettingsAccessPolicy.SettingKeyFor(area), ct);
            return Results.Ok(new SettingsAccessLevelDto(SettingsAccessPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> UpdateAreaAsync(
            RestrictedSettings area,
            UpdateSettingsAccessRequest request,
            IAppSettingRepository settings,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // Validated at the API boundary. Storing anything else would silently fall back to SuperAdmin on read.
            string level = request.Access;
            if (level != ProviderEditAccessLevels.Admin && level != ProviderEditAccessLevels.SuperAdmin)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Access)] = ["Must be Admin or SuperAdmin."],
                });
            }

            await settings.SetAsync(SettingsAccessPolicy.SettingKeyFor(area), level, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.SettingsPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"{area}: {level}", isUrgent: true, ct);

            return Results.Ok(new SettingsAccessLevelDto(level));
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
