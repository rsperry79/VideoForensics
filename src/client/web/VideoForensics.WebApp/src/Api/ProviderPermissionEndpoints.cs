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
    /// Reads and changes who may edit provider accounts who may change report settings, who may change email settings, and who may change notification settings. Reading is open to Admin and up so the UI can show the current
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

            _ = group.MapGet("/reports", GetReportsAsync)
                .WithName("GetReportPermissions");

            _ = group.MapPut("/reports", UpdateReportsAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("UpdateReportPermissions");

            _ = group.MapGet("/email", GetEmailAsync)
                .WithName("GetEmailPermissions");

            _ = group.MapPut("/email", UpdateEmailAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("UpdateEmailPermissions");

            _ = group.MapGet("/notifications", GetNotificationsAsync)
                .WithName("GetNotificationPermissions");

            _ = group.MapPut("/notifications", UpdateNotificationsAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("UpdateNotificationPermissions");
        }

        private static async Task<IResult> GetAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(ProviderAccountEditPolicy.SettingKey, ct);
            return Results.Ok(new ProviderPermissionsDto(ProviderAccountEditPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> GetReportsAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(ReportSettingsPolicy.SettingKey, ct);
            return Results.Ok(new ReportPermissionsDto(ReportSettingsPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> UpdateReportsAsync(
            UpdateReportPermissionsRequest request,
            IAppSettingRepository settings,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string level = request.ReportSettingsAccess;
            if (level != ProviderEditAccessLevels.Admin && level != ProviderEditAccessLevels.SuperAdmin)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.ReportSettingsAccess)] = ["Must be Admin or SuperAdmin."],
                });
            }

            await settings.SetAsync(ReportSettingsPolicy.SettingKey, level, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.ReportPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"ReportSettings: {level}", isUrgent: true, ct);

            return Results.Ok(new ReportPermissionsDto(level));
        }

        private static async Task<IResult> GetEmailAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(EmailSettingsPolicy.SettingKey, ct);
            return Results.Ok(new EmailPermissionsDto(EmailSettingsPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> UpdateEmailAsync(
            UpdateEmailPermissionsRequest request,
            IAppSettingRepository settings,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string level = request.EmailSettingsAccess;
            if (level != ProviderEditAccessLevels.Admin && level != ProviderEditAccessLevels.SuperAdmin)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.EmailSettingsAccess)] = ["Must be Admin or SuperAdmin."],
                });
            }

            await settings.SetAsync(EmailSettingsPolicy.SettingKey, level, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.EmailPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"EmailSettings: {level}", isUrgent: true, ct);

            return Results.Ok(new EmailPermissionsDto(level));
        }

        private static async Task<IResult> GetNotificationsAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(NotificationSettingsPolicy.SettingKey, ct);
            return Results.Ok(new NotificationPermissionsDto(NotificationSettingsPolicy.NormalizeLevel(stored)));
        }

        private static async Task<IResult> UpdateNotificationsAsync(
            UpdateNotificationPermissionsRequest request,
            IAppSettingRepository settings,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            string level = request.NotificationSettingsAccess;
            if (level != ProviderEditAccessLevels.Admin && level != ProviderEditAccessLevels.SuperAdmin)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.NotificationSettingsAccess)] = ["Must be Admin or SuperAdmin."],
                });
            }

            await settings.SetAsync(NotificationSettingsPolicy.SettingKey, level, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.NotificationPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"NotificationSettings: {level}", isUrgent: true, ct);

            return Results.Ok(new NotificationPermissionsDto(level));
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
