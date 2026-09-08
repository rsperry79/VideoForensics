using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Minimal API surface for forensics configuration read operations. Exposes only client-safe
    /// operational settings via ClientConfigDto - infrastructure details and credentials are excluded.
    /// All endpoints require paired-device authentication (plan §4/§6).
    /// </summary>
    public static class ConfigEndpoints
    {
        public static void MapConfigEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/v1/config");

            _ = group.MapGet("/", (IForensicsConfiguration config) =>
                Results.Ok(config.ToDto()))
                .RequireAuthorization()
                .WithSummary("Get forensics configuration")
                .WithDescription("Retrieves current server configuration settings safe for client access, including report generation flags, redaction levels, and operational parameters.")
                .RequireRateLimiting("media");
        }
    }
}
