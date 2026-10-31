using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Logging.Providers;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Reads and sets whether the named pipe logger is open, so the LogViewer console can show server logs. The pipe is
    /// closed by default because any local user can connect to it. Gated like the Infrastructure page.
    /// </summary>
    public static class LogPipeEndpoints
    {
        /// <summary>App-setting key holding "true" or "false". Applied at startup and on every change.</summary>
        public const string SettingKey = "Diagnostics.NamedPipeLogger";

        public static void MapLogPipeEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/api/v1/diagnostics/log-pipe", GetAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireSettingsAccess(RestrictedSettings.Infrastructure)
                .WithSummary("Get whether the named pipe logger is open")
                .Produces<LogPipeSettingDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status403Forbidden);

            _ = app.MapPost("/api/v1/diagnostics/log-pipe", SetAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireSettingsAccess(RestrictedSettings.Infrastructure)
                .WithSummary("Open or close the named pipe logger")
                .Produces<LogPipeSettingDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status403Forbidden);
        }

        /// <summary>Reads the stored setting. A missing or unrecognised value means closed.</summary>
        public static bool IsEnabled(string? stored)
        {
            return string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<IResult> GetAsync(IAppSettingRepository settings, CancellationToken ct)
        {
            string? stored = await settings.GetAsync(SettingKey, ct);
            return Results.Ok(new LogPipeSettingDto(IsEnabled(stored)));
        }

        private static async Task<IResult> SetAsync(
            LogPipeSettingDto request,
            IAppSettingRepository settings,
            NamedPipeLogGate gate,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            await settings.SetAsync(SettingKey, request.Enabled ? "true" : "false", ct);
            gate.SetOpen(request.Enabled);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.SettingsPermissionsUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"LogPipe: {(request.Enabled ? "open" : "closed")}", isUrgent: true, ct);

            return Results.Ok(new LogPipeSettingDto(request.Enabled));
        }
    }

    /// <summary>Whether the named pipe logger is open.</summary>
    /// <param name="Enabled">True when the LogViewer pipe is open.</param>
    public record LogPipeSettingDto(bool Enabled);
}
