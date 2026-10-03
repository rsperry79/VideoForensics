using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Admin endpoints for managing login method (auth method) settings: which authentication methods
    /// (password and passkey) are available. The public GET /api/v1/auth/methods endpoint (anonymous)
    /// returns only enabled flags. The admin endpoints (GET/PUT /api/v1/auth-methods/settings) require
    /// SuperAdmin+Local authorization.
    /// </summary>
    public static class AuthMethodEndpoints
    {
        public static void MapAuthMethodEndpoints(this WebApplication app)
        {
            // Public endpoint: get enabled auth methods (no authentication required)
            _ = app.MapGet("/api/v1/auth/methods", GetAuthMethodsAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Get currently-enabled authentication methods");

            // Admin endpoints: get/update auth method settings
            RouteGroupBuilder group = app.MapGroup("/api/v1/auth-methods/settings")
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", GetSettingsAsync)
                .WithName("GetAuthMethodSettings");

            _ = group.MapPut("/", UpdateSettingsAsync)
                .WithName("UpdateAuthMethodSettings")
                .AddEndpointFilter<StepUpEndpointFilter>();
        }

        private static async Task<IResult> GetAuthMethodsAsync(
            IAuthMethodSettingsService authMethodsService,
            CancellationToken ct)
        {
            var settings = await authMethodsService.GetAsync(ct);

            var authMethodsDto = new AuthMethodsDto(
                Password: settings.PasswordEnabled,
                Passkey: settings.PasskeyEnabled);

            return Results.Ok(authMethodsDto);
        }

        private static async Task<IResult> GetSettingsAsync(
            IAuthMethodSettingsService authMethodsService,
            CancellationToken ct)
        {
            var settings = await authMethodsService.GetAsync(ct);
            return Results.Ok(settings);
        }

        private static async Task<IResult> UpdateSettingsAsync(
            UpdateAuthMethodSettingsRequest request,
            IAuthMethodSettingsService authMethodsService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            bool updated = await authMethodsService.UpdateAsync(request, ct);
            if (!updated)
            {
                return Results.BadRequest(new { error = "Cannot disable both password and passkey authentication methods. At least one must remain enabled." });
            }

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            string details = BuildAuditDetails(request);
            await auditLog.LogAsync(SecurityAuditEventTypes.AuthMethodsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);

            var settings = await authMethodsService.GetAsync(ct);
            return Results.Ok(settings);
        }

        private static string BuildAuditDetails(UpdateAuthMethodSettingsRequest request)
        {
            var parts = new List<string>();

            if (request.PasswordEnabled.HasValue)
                parts.Add($"Password: {(request.PasswordEnabled.Value ? "enabled" : "disabled")}");

            if (request.PasskeyEnabled.HasValue)
                parts.Add($"Passkey: {(request.PasskeyEnabled.Value ? "enabled" : "disabled")}");

            return string.Join(", ", parts);
        }
    }
}
