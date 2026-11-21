using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>The time-lapse watchdog: an account whose check has not completed for twice its interval is reported once.</summary>
    public class TimelapseCheckWatchdogTests
    {
        private static readonly DateTime T = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        [Fact]
        public void TakeNewStalls_BeforeTwiceTheInterval_ReportsNothing()
        {
            var watchdog = new TimelapseCheckWatchdog();
            watchdog.Attempted(Guid.NewGuid(), "Ring", Interval, T);

            Assert.Empty(watchdog.TakeNewStalls(T.AddMinutes(29)));
        }

        [Fact]
        public void TakeNewStalls_PastTwiceTheInterval_ReportsOnceUntilItCompletes()
        {
            var watchdog = new TimelapseCheckWatchdog();
            watchdog.Attempted(Guid.NewGuid(), "Ring", Interval, T);

            IReadOnlyList<TimelapseCheckStall> first = watchdog.TakeNewStalls(T.AddMinutes(31));
            IReadOnlyList<TimelapseCheckStall> again = watchdog.TakeNewStalls(T.AddMinutes(40));

            TimelapseCheckStall stall = Assert.Single(first);
            Assert.Equal("Ring", stall.ProviderName);
            Assert.Equal(Interval, stall.CheckInterval);
            Assert.Empty(again);
        }

        [Fact]
        public void Completed_ResetsTheClock_SoAStallIsMeasuredFromTheLastCompletion()
        {
            var watchdog = new TimelapseCheckWatchdog();
            Guid account = Guid.NewGuid();
            watchdog.Attempted(account, "Ring", Interval, T);
            watchdog.Completed(account, T.AddMinutes(5));

            Assert.Empty(watchdog.TakeNewStalls(T.AddMinutes(35)));
            Assert.Single(watchdog.TakeNewStalls(T.AddMinutes(36)));
        }

        [Fact]
        public void Completed_AfterAStall_AllowsAnotherReportWhenItStallsAgain()
        {
            var watchdog = new TimelapseCheckWatchdog();
            Guid account = Guid.NewGuid();
            watchdog.Attempted(account, "Ring", Interval, T);
            _ = watchdog.TakeNewStalls(T.AddMinutes(31));
            watchdog.Completed(account, T.AddMinutes(32));

            Assert.Single(watchdog.TakeNewStalls(T.AddMinutes(65)));
        }
    }
}
