using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using VideoForensics.Providers.Ring.Exceptions;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Probe-only HTTP endpoints under /api/v1/probe, used by an external harness to measure Ring snapshot
    /// behaviour through the running app. The harness never sees stored Ring credentials: the server uses
    /// the account's existing Session.
    ///
    /// Off unless SnapshotProbe:Enabled is true AND SnapshotProbe:Token is non-empty. When off, no routes
    /// are registered. Every request must send X-Probe-Token matching SnapshotProbe:Token; anything else
    /// gets 404 so the existence of the feature is not revealed.
    /// </summary>
    public static class SnapshotProbeEndpoints
    {
        /// <summary>Request header carrying the probe token.</summary>
        public const string TokenHeader = "X-Probe-Token";

        private const string EnabledKey = "SnapshotProbe:Enabled";
        private const string TokenKey = "SnapshotProbe:Token";
        private const string LoggerCategory = "VideoForensics.Client.WebApp.SnapshotProbe";

        /// <summary>
        /// Registers the probe backend when the probe is enabled. Call from Program.cs before the host is built.
        /// </summary>
        public static void AddSnapshotProbe(this IServiceCollection services, IConfiguration configuration)
        {
            if (!IsEnabled(configuration))
            {
                return;
            }

            _ = services.AddSingleton<ISnapshotProbeBackend, SessionSnapshotProbeBackend>();
        }

        /// <summary>
        /// Maps the probe routes when the probe is enabled; does nothing otherwise.
        /// </summary>
        public static void MapSnapshotProbeEndpoints(this WebApplication app)
        {
            if (!IsEnabled(app.Configuration))
            {
                return;
            }

            byte[] expectedToken = Encoding.UTF8.GetBytes(app.Configuration[TokenKey]!);
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

            RouteGroupBuilder group = app.MapGroup("/api/v1/probe")
                .AddEndpointFilter(async (context, next) =>
                {
                    return TokenMatches(context.HttpContext, expectedToken) ? await next(context) : Results.NotFound();
                });

            _ = group.MapGet("/ring/{providerAccountId:guid}/doorbots/{doorbotId:long}/latest-snapshot",
                    (Guid providerAccountId, long doorbotId, ISnapshotProbeBackend backend, HttpContext http, CancellationToken ct) =>
                        Guard(http, logger, "latest-snapshot", async () =>
                        {
                            byte[] bytes = await backend.GetLatestSnapshotAsync(providerAccountId, doorbotId, ct);
                            string sha256Prefix = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..12];
                            return Results.Ok(new
                            {
                                httpStatus = 200,
                                bytes = bytes.Length,
                                sha256Prefix,
                                utc = DateTimeOffset.UtcNow,
                            });
                        }))
                .WithName("SnapshotProbeLatestSnapshot");

            _ = group.MapPost("/ring/{providerAccountId:guid}/doorbots/{doorbotId:long}/fresh-snapshot",
                    (Guid providerAccountId, long doorbotId, ISnapshotProbeBackend backend, HttpContext http, CancellationToken ct) =>
                        Guard(http, logger, "fresh-snapshot", async () =>
                        {
                            await backend.UpdateSnapshotAsync(providerAccountId, doorbotId, ct);
                            return Results.NoContent();
                        }))
                .WithName("SnapshotProbeFreshSnapshot");

            _ = group.MapGet("/ring/{providerAccountId:guid}/doorbots/{doorbotId:long}/battery",
                    (Guid providerAccountId, long doorbotId, ISnapshotProbeBackend backend, HttpContext http, CancellationToken ct) =>
                        Guard(http, logger, "battery", async () =>
                        {
                            SnapshotProbeBattery battery = await backend.GetBatteryAsync(providerAccountId, doorbotId, ct);
                            // "batteries" is not a field on the current Ring device model; it is always null here.
                            return Results.Ok(new Dictionary<string, object?>
                            {
                                ["batteries"] = null,
                                ["battery_life"] = battery.BatteryLife,
                                ["battery_voltage"] = battery.BatteryVoltage,
                            });
                        }))
                .WithName("SnapshotProbeBattery");
        }

        private static bool IsEnabled(IConfiguration configuration)
        {
            return bool.TryParse(configuration[EnabledKey], out bool enabled)
                && enabled
                && !string.IsNullOrEmpty(configuration[TokenKey]);
        }

        /// <summary>Constant-time comparison so the token cannot be recovered by timing the response.</summary>
        private static bool TokenMatches(HttpContext context, byte[] expectedToken)
        {
            if (!context.Request.Headers.TryGetValue(TokenHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return false;
            }

            byte[] presented = Encoding.UTF8.GetBytes(values.ToString());
            return presented.Length == expectedToken.Length
                && CryptographicOperations.FixedTimeEquals(presented, expectedToken);
        }

        /// <summary>
        /// Maps probe failures to status codes. Logs use the provider name, never the account GUID, and never
        /// the exception message (it may carry tokens); only the exception type is logged.
        /// </summary>
        private static async Task<IResult> Guard(HttpContext http, ILogger logger, string operation, Func<Task<IResult>> action)
        {
            try
            {
                return await action();
            }
            catch (SnapshotProbeSessionUnavailableException ex)
            {
                logger.LogWarning("Snapshot probe {Operation} unavailable for Ring account: {ExceptionType}", operation, ex.GetType().Name);
                return Results.Json(new { message = "No active Ring session for that account." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (ThrottledException ex)
            {
                logger.LogWarning("Snapshot probe {Operation} throttled by Ring for Ring account", operation);
                if (ex.RetryAfter is TimeSpan retryAfter)
                {
                    http.Response.Headers.RetryAfter = Math.Max(0, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return Results.Json(new { message = "Ring is throttling requests. Try again later." }, statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Snapshot probe {Operation} failed for Ring account: {ExceptionType}", operation, ex.GetType().Name);
                return Results.Json(new { message = "Ring request failed. See server log." }, statusCode: StatusCodes.Status502BadGateway);
            }
        }
    }
}
