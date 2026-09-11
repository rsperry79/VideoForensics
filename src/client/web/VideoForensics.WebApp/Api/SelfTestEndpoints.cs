using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Ring self-test smoke-test endpoints (fire-and-forget + poll pattern).
    ///
    /// FIRE-AND-FORGET DESIGN: POST /run triggers a long-running self-test operation that runs
    /// as a background task on the server and returns 202 Accepted immediately. Progress is polled
    /// via GET /status (cheap synchronous check) and final results via GET /result (204 while
    /// still running, 200 with the IndexDocument once complete).
    ///
    /// AUTH-TIERING: All endpoints require baseline paired-device authentication (RequireAuthorization).
    /// POST /run additionally gates destructive endpoints: if request.Destructive is true, the caller
    /// must have SuperAdmin role AND local network access (SuperAdminLocal policy equivalent). This
    /// check is performed inside the handler body since per-request-body conditional policies cannot
    /// be declared statically via RequireAuthorization().
    /// </summary>
    public static class SelfTestEndpoints
    {
        public static void MapSelfTestEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/v1/selftest").RequireAuthorization();

            // List all available self-test endpoints.
            _ = group.MapGet("/", async (
                IRingSelfTestService selfTestService,
                CancellationToken ct) =>
            {
                var endpoints = await selfTestService.ListEndpointsAsync(ct);
                return Results.Ok(endpoints);
            })
            .WithSummary("List self-test endpoints")
            .WithDescription("Returns a list of available Ring self-test endpoints with metadata (name, scope, whether destructive/physical). Used to populate the endpoint selector UI.");

            // Trigger a self-test run as a background task.
            _ = group.MapPost("/run", async (
                SelfTestRunRequestDto request,
                IRingSelfTestService selfTestService,
                HttpContext context,
                CancellationToken ct) =>
            {
                // Per-request-body auth gate: if destructive endpoints are requested, enforce SuperAdminLocal.
                // This check must happen inside the handler body since we can't conditionally apply
                // RequireAuthorization() based on the request body - see EvidenceEndpoints' /export for
                // the same asymmetric narrowing pattern.
                if (request.Destructive)
                {
                    var roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
                    var tierClaim = context.User.FindFirst(VideoForensicsClaimTypes.NetworkTier)?.Value;

                    bool isSuperAdmin = roleClaim != null && Enum.TryParse<OperatorRole>(roleClaim, out var role) && role >= OperatorRole.SuperAdmin;
                    bool isLocal = tierClaim != null && Enum.TryParse<NetworkTier>(tierClaim, out var tier) && tier == NetworkTier.Local;

                    if (!isSuperAdmin || !isLocal)
                    {
                        return Results.Json(
                            new { error = "Destructive self-test runs require SuperAdmin role and local network access." },
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                }

                var result = await selfTestService.StartRunAsync(request, ct);
                return result.Accepted
                    ? Results.Accepted(null, result)
                    : Results.Conflict(result);
            })
            .WithSummary("Start a self-test run")
            .WithDescription("Triggers a self-test run against the selected Ring endpoints. Returns 202 Accepted if queued, 409 Conflict if a run is already in progress. " +
                "If request.Destructive is true, additionally requires SuperAdmin role and local network access.");

            // Poll the current run status synchronously.
            _ = group.MapGet("/status", async (
                IRingSelfTestService selfTestService,
                CancellationToken ct) =>
            {
                var status = await selfTestService.GetStatusAsync(ct);
                return Results.Ok(status);
            })
            .WithSummary("Get self-test run status")
            .WithDescription("Returns the current status of the most recent self-test run (Idle, Running, Completed, or Failed), along with timestamps and error details if applicable.");

            // Get completed results once the run finishes.
            _ = group.MapGet("/result", async (
                IRingSelfTestService selfTestService,
                CancellationToken ct) =>
            {
                var result = await selfTestService.GetResultAsync(ct);
                return result == null
                    ? Results.NoContent()
                    : Results.Ok(result);
            })
            .WithSummary("Get self-test run results")
            .WithDescription("Returns the completed SelfTestResultDto from the most recent self-test run (including summary and per-call details), or 204 No Content if no run has completed yet.");
        }
    }
}
