using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Liveness endpoint (plan §5.8) for third-party uptime monitors (e.g. Uptime Kuma).
    /// Deliberately unauthenticated (a monitor shouldn't need pairing credentials). Always answers HTTP 200 with a JSON
    /// body: a monitor reads <c>status</c> from the body, so a failing service must not change the HTTP code. The body
    /// carries the build version and per-service state, with fixed error categories only, never exception text.
    /// </summary>
    public static class HealthEndpoints
    {
        public static void MapVideoForensicsHealthEndpoints(this WebApplication app)
        {
            _ = app.MapHealthChecks("/healthz", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    CancellationToken ct = context.RequestAborted;
                    string version = await context.RequestServices
                        .GetRequiredService<ISystemVersionProvider>()
                        .GetCurrentVersionAsync(ct);
                    IReadOnlyList<ServiceStatus> services = context.RequestServices
                        .GetRequiredService<ThreadHeartbeats>()
                        .Snapshot(context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), ServiceHealthCheck.DefaultStaleAfter);

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(HealthReportJson.Write(report.Status, version, services), ct);
                }
            });
        }
    }
}
