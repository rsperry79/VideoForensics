using Microsoft.Extensions.Diagnostics.HealthChecks;

using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>Health check that is Unhealthy while any background service is Stale or Failed.</summary>
    public class ServiceHealthCheckTests
    {
        private static readonly DateTimeOffset T = new(2026, 11, 7, 2, 0, 0, TimeSpan.Zero);

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private static Task<HealthCheckResult> Check(ThreadHeartbeats heartbeats, DateTimeOffset now) =>
            new ServiceHealthCheck(heartbeats, new FixedTimeProvider(now), ServiceHealthCheck.DefaultStaleAfter)
                .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        [Fact]
        public async Task CheckHealthAsync_BeatRecently_ReturnsHealthy()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("ScheduledSyncService", T, TimeSpan.FromMinutes(1));

            HealthCheckResult result = await Check(heartbeats, T);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_RecordedError_ReturnsUnhealthyNamingServiceNotError()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.RecordError("ScheduledSyncService", T, "sync failed");

            HealthCheckResult result = await Check(heartbeats, T);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("ScheduledSyncService", result.Description ?? string.Empty);
            Assert.DoesNotContain("sync failed", result.Description ?? string.Empty);
        }

        [Fact]
        public async Task CheckHealthAsync_RegisteredOnly_ReturnsHealthy()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Register("ScheduledSyncService");

            HealthCheckResult result = await Check(heartbeats, T);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_BeatLongAgo_ReturnsUnhealthy()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("ScheduledSyncService", T.AddHours(-1));

            HealthCheckResult result = await Check(heartbeats, T);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }
    }
}
