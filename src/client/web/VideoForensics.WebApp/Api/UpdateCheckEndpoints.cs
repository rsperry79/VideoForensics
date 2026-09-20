using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Update-check service endpoints: read the current update state and trigger an immediate check.
    /// All endpoints are SuperAdmin-only via Local network tier, matching the operational scope
    /// of other system configuration and background-service controls.
    /// </summary>
    public static class UpdateCheckEndpoints
    {
        public static void MapUpdateCheckEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/update-check").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", GetUpdateCheckState)
                .WithSummary("Get update check state")
                .WithDescription("Returns the current state of update checking, including whether a newer version is available, the latest version, and any recent error messages.");

            _ = group.MapPost("/check-now", CheckNow)
                .WithSummary("Trigger an immediate update check")
                .WithDescription("Triggers an immediate update check without waiting for the periodic timer, then returns the refreshed state.");
        }

        private static IResult GetUpdateCheckState(IUpdateCheckService svc)
        {
            return Results.Ok(svc.GetState().ToDto());
        }

        private static async Task<IResult> CheckNow(IUpdateCheckService svc, CancellationToken ct)
        {
            await svc.TriggerCheckNowAsync(ct);
            return Results.Ok(svc.GetState().ToDto());
        }
    }
}
