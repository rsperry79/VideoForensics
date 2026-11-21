using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// ASP.NET health check over <see cref="ThreadHeartbeats"/>. Reports <see cref="HealthStatus.Unhealthy"/> while any
    /// background service is <c>Stale</c> or <c>Failed</c>. The description lists service names and states only, never
    /// <see cref="ServiceStatus.LastError"/> text, because the health endpoint is anonymous.
    /// </summary>
    /// <remarks>
    /// <c>NotStarted</c> and <c>Running</c> services are Healthy. A service that has not started yet must not fail the
    /// check during the startup window.
    /// </remarks>
    public sealed class ServiceHealthCheck : IHealthCheck
    {
        /// <summary>Production threshold after which a service that has stopped beating is considered Stale.</summary>
        public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(5);

        private readonly ThreadHeartbeats _heartbeats;
        private readonly TimeProvider _time;
        private readonly TimeSpan _staleAfter;

        /// <summary>Creates the check over the shared heartbeats.</summary>
        /// <param name="heartbeats">The heartbeats the background services write to.</param>
        /// <param name="time">The clock used to judge staleness.</param>
        /// <param name="staleAfter">How long a service may go without beating before it is Stale.</param>
        public ServiceHealthCheck(ThreadHeartbeats heartbeats, TimeProvider time, TimeSpan staleAfter)
        {
            _heartbeats = heartbeats ?? throw new ArgumentNullException(nameof(heartbeats));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _staleAfter = staleAfter;
        }

        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ServiceStatus> snapshot = _heartbeats.Snapshot(_time.GetUtcNow(), _staleAfter);

            List<string> unhealthy = snapshot
                .Where(service => service.State is "Stale" or "Failed")
                .Select(service => $"{service.Name}: {service.State}")
                .ToList();

            HealthCheckResult result = unhealthy.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(string.Join("; ", unhealthy));

            return Task.FromResult(result);
        }
    }
}
