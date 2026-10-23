using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>A background loop that stops ticking is reported once, so a hung service does not go unnoticed.</summary>
    public class ThreadWatchdogTests
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

        [Fact]
        public void Heartbeats_RecentBeat_IsNotStale()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("SnapshotCaptureService", Start);

            IReadOnlyList<string> stale = heartbeats.StaleServices(Start.AddMinutes(4), StaleAfter);

            Assert.Empty(stale);
        }

        [Fact]
        public void Heartbeats_NoBeatForLongerThanThreshold_IsStale()
        {
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("SnapshotCaptureService", Start);

            IReadOnlyList<string> stale = heartbeats.StaleServices(Start.AddMinutes(6), StaleAfter);

            Assert.Equal(new[] { "SnapshotCaptureService" }, stale);
        }

        [Fact]
        public void Watchdog_StaleService_LogsErrorOnceUntilItBeatsAgain()
        {
            var clock = new FakeTime(Start);
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("ScheduledSyncService", Start);
            var logger = new Mock<ILogger<ThreadWatchdogService>>();
            var watchdog = new ThreadWatchdogService(heartbeats, logger.Object, clock, StaleAfter);

            clock.Advance(TimeSpan.FromMinutes(6));
            watchdog.Check();
            watchdog.Check();
            Assert.Equal(1, CountErrors(logger));

            heartbeats.Beat("ScheduledSyncService", clock.GetUtcNow());
            watchdog.Check();
            clock.Advance(TimeSpan.FromMinutes(6));
            watchdog.Check();
            Assert.Equal(2, CountErrors(logger));
        }

        [Fact]
        public void Heartbeats_SlowLoop_IsNotStaleUntilItMissesTwoIntervals()
        {
            // A 15-minute loop is fine at 20 minutes; a five-minute threshold alone would wrongly flag it.
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("DeviceHealthSyncService", Start, TimeSpan.FromMinutes(15));

            Assert.Empty(heartbeats.StaleServices(Start.AddMinutes(20), StaleAfter));
            Assert.Equal(new[] { "DeviceHealthSyncService" }, heartbeats.StaleServices(Start.AddMinutes(31), StaleAfter));
        }

        [Fact]
        public void Watchdog_AllServicesBeating_LogsNothing()
        {
            var clock = new FakeTime(Start);
            var heartbeats = new ThreadHeartbeats();
            heartbeats.Beat("UpdateCheckService", Start);
            var logger = new Mock<ILogger<ThreadWatchdogService>>();
            var watchdog = new ThreadWatchdogService(heartbeats, logger.Object, clock, StaleAfter);

            clock.Advance(TimeSpan.FromMinutes(1));
            watchdog.Check();

            Assert.Equal(0, CountErrors(logger));
        }

        private static int CountErrors(Mock<ILogger<ThreadWatchdogService>> logger)
        {
            return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == LogLevel.Error);
        }

        private sealed class FakeTime : TimeProvider
        {
            private DateTimeOffset _now;

            public FakeTime(DateTimeOffset start)
            {
                _now = start;
            }

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now = _now.Add(by);
        }
    }
}
