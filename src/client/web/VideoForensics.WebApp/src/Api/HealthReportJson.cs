using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Builds the JSON body for the anonymous /healthz endpoint. A pure function over its inputs, so it can be tested
    /// without a host. The body carries the build version and per-service state; <see cref="ServiceStatus.LastError"/> is
    /// written as-is because it is a fixed category, never exception text.
    /// </summary>
    public static class HealthReportJson
    {
        /// <summary>Serializes the health status, build version and background-service states to a JSON object.</summary>
        /// <param name="status">Overall health. Anything other than <see cref="HealthStatus.Healthy"/> is written as "Unhealthy".</param>
        /// <param name="version">Build version string from <c>ISystemVersionProvider</c>.</param>
        /// <param name="services">Service states from <c>ThreadHeartbeats.Snapshot</c>, written in the order given.</param>
        /// <returns>The JSON text.</returns>
        public static string Write(HealthStatus status, string version, IReadOnlyList<ServiceStatus> services)
        {
            ArgumentNullException.ThrowIfNull(version);
            ArgumentNullException.ThrowIfNull(services);

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("status", status == HealthStatus.Healthy ? "Healthy" : "Unhealthy");
                writer.WriteString("version", version);
                writer.WriteStartArray("services");
                foreach (ServiceStatus service in services)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", service.Name);
                    writer.WriteString("state", service.State);
                    // UtcDateTime gives the ISO-8601 "Z" form; a null run time must be written as JSON null, not omitted.
                    if (service.LastRunUtc is DateTimeOffset lastRun)
                    {
                        writer.WriteString("lastRunUtc", lastRun.UtcDateTime);
                    }
                    else
                    {
                        writer.WriteNull("lastRunUtc");
                    }

                    if (service.LastError is null)
                    {
                        writer.WriteNull("lastError");
                    }
                    else
                    {
                        writer.WriteString("lastError", service.LastError);
                    }

                    // Per-item failures (for the scheduler, accounts that failed on the last tick). The state is unchanged by them.
                    writer.WriteNumber("failedItems", service.FailedItems);

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
