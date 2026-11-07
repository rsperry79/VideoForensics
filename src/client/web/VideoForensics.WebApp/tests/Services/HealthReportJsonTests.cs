using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.WebApp.Api;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    /// <summary>Shape of the anonymous /healthz JSON body.</summary>
    public class HealthReportJsonTests
    {
        private static readonly DateTimeOffset LastRun = new(2026, 11, 6, 10, 15, 30, TimeSpan.Zero);

        [Fact]
        public void HealthReportJson_Write_HealthyWithTwoServices_ShapesExactly()
        {
            var services = new List<ServiceStatus>
            {
                new("ScheduledSyncService", "Running", LastRun, null),
                new("SnapshotCaptureService", "NotStarted", null, null),
            };

            string json = HealthReportJson.Write(HealthStatus.Healthy, "1.0.5", services);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            Assert.Equal("Healthy", root.GetProperty("status").GetString());
            Assert.Equal("1.0.5", root.GetProperty("version").GetString());

            JsonElement list = root.GetProperty("services");
            Assert.Equal(2, list.GetArrayLength());
            Assert.Equal("ScheduledSyncService", list[0].GetProperty("name").GetString());
            Assert.Equal("Running", list[0].GetProperty("state").GetString());
            Assert.Equal("2026-11-06T10:15:30Z", list[0].GetProperty("lastRunUtc").GetString());
            Assert.Equal(JsonValueKind.Null, list[0].GetProperty("lastError").ValueKind);
            Assert.Equal("SnapshotCaptureService", list[1].GetProperty("name").GetString());
            Assert.Equal("NotStarted", list[1].GetProperty("state").GetString());
        }

        [Fact]
        public void HealthReportJson_Write_UnhealthyWithStaleService_WritesUnhealthyAndStale()
        {
            var services = new List<ServiceStatus>
            {
                new("ScheduledSyncService", "Stale", LastRun, null),
            };

            string json = HealthReportJson.Write(HealthStatus.Unhealthy, "1.0.5", services);

            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("Stale", doc.RootElement.GetProperty("services")[0].GetProperty("state").GetString());
        }

        [Fact]
        public void HealthReportJson_Write_LastError_PassesThroughCategoryOnly()
        {
            var services = new List<ServiceStatus>
            {
                new("ScheduledSyncService", "Failed", LastRun, "sync failed"),
            };

            string json = HealthReportJson.Write(HealthStatus.Unhealthy, "1.0.5", services);

            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.Equal("sync failed", doc.RootElement.GetProperty("services")[0].GetProperty("lastError").GetString());
        }

        [Fact]
        public void HealthReportJson_Write_NullLastRun_WritesJsonNull()
        {
            var services = new List<ServiceStatus>
            {
                new("ScheduledSyncService", "NotStarted", null, null),
            };

            string json = HealthReportJson.Write(HealthStatus.Healthy, "1.0.5", services);

            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("services")[0].GetProperty("lastRunUtc").ValueKind);
        }
    }
}
