using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Liveness endpoint (plan §5.8) for third-party uptime monitors (e.g. Uptime Kuma).
    /// Deliberately unauthenticated (a monitor shouldn't need pairing credentials) and deliberately
    /// minimal - a bare Healthy/Unhealthy status only, no diagnostic detail (device counts, DB
    /// state, etc.) that an unauthenticated caller shouldn't see.
    /// </summary>
    public static class HealthEndpoints
    {
        public static void MapVideoForensicsHealthEndpoints(this WebApplication app)
        {
            app.MapHealthChecks("/healthz", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(report.Status.ToString());
                }
            });
        }
    }
}
