using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>A non-image snapshot warning is logged once per window, not on every pull.</summary>
    public class RingNonImageWarningTests
    {
        private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        [Fact]
        public void NeverWarned_Warns()
        {
            Assert.True(RingMediaDownloadService.ShouldWarnNonImage(null, Now, Window));
        }

        [Fact]
        public void WarnedInsideTheWindow_DoesNotWarnAgain()
        {
            Assert.False(RingMediaDownloadService.ShouldWarnNonImage(Now.AddMinutes(-14), Now, Window));
        }

        [Fact]
        public void WindowElapsed_WarnsAgain()
        {
            Assert.True(RingMediaDownloadService.ShouldWarnNonImage(Now.AddMinutes(-15), Now, Window));
        }

        [Fact]
        public void LongerWindow_KeepsTheWarningQuieter()
        {
            // The window follows the account's roll-up interval, so a 30-minute roll-up warns half as often.
            Assert.False(RingMediaDownloadService.ShouldWarnNonImage(Now.AddMinutes(-20), Now, TimeSpan.FromMinutes(30)));
        }
    }
}
