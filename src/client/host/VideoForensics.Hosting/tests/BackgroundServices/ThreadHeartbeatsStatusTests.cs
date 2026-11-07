using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>Per-service last run, last error and state, read by the health endpoint.</summary>
    public class ThreadHeartbeatsStatusTests
    {
        private static readonly DateTimeOffset T = new(2026, 11, 7, 2, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

        private static ServiceStatus Find(IReadOnlyList<ServiceStatus> list, string name) =>
            Assert.Single(list, s => s.Name == name);

        [Fact]
        public void RecordRun_ThenSnapshot_ReportsRunningWithLastRun()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.RecordRun("ScheduledSyncService", T);

            ServiceStatus status = Find(heartbeats.Snapshot(T, StaleAfter), "ScheduledSyncService");

            Assert.Equal("Running", status.State);
            Assert.Equal(T, status.LastRunUtc);
            Assert.Null(status.LastError);
        }

        [Fact]
        public void RecordError_ThenSnapshot_ReportsFailedWithCategoryOnly()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.RecordRun("SnapshotCaptureService", T);
            heartbeats.RecordError("SnapshotCaptureService", T.AddMinutes(1), "capture failed");

            ServiceStatus status = Find(heartbeats.Snapshot(T.AddMinutes(1), StaleAfter), "SnapshotCaptureService");

            Assert.Equal("Failed", status.State);
            Assert.Equal("capture failed", status.LastError);
            Assert.Equal(T, status.LastRunUtc);
        }

        [Fact]
        public void RecordRun_AfterAnError_ClearsTheErrorAndReportsRunning()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.RecordError("ScheduledSyncService", T, "sync failed");
            heartbeats.RecordRun("ScheduledSyncService", T.AddMinutes(1));

            ServiceStatus status = Find(heartbeats.Snapshot(T.AddMinutes(1), StaleAfter), "ScheduledSyncService");

            Assert.Equal("Running", status.State);
            Assert.Null(status.LastError);
        }

        [Fact]
        public void Register_NeverRun_ReportsNotStarted()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Register("ThreadWatchdogService");

            ServiceStatus status = Find(heartbeats.Snapshot(T, StaleAfter), "ThreadWatchdogService");

            Assert.Equal("NotStarted", status.State);
            Assert.Null(status.LastRunUtc);
        }

        [Fact]
        public void Snapshot_BeatOlderThanItsThreshold_ReportsStale()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("CameraBitrateCalibrationService", T, TimeSpan.FromMinutes(1));

            ServiceStatus status = Find(heartbeats.Snapshot(T.AddMinutes(10), StaleAfter), "CameraBitrateCalibrationService");

            Assert.Equal("Stale", status.State);
        }

        [Fact]
        public void Snapshot_ListsEveryKnownServiceInNameOrder()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.RecordRun("b-service", T);
            heartbeats.Register("a-service");

            IReadOnlyList<ServiceStatus> list = heartbeats.Snapshot(T, StaleAfter);

            Assert.Equal(new[] { "a-service", "b-service" }, list.Select(s => s.Name).ToArray());
        }
    }
}
