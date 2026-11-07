using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>The horizon: how far a time lapse may be planned, from the last clean event pull and the last snapshot sync.</summary>
    public class TimelapseHorizonTests
    {
        private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Lag = TimeSpan.FromMinutes(10);

        [Fact]
        public void Compute_PullAndSnapshotBothKnown_ReturnsEarlierOfPullMinusLagAndSnapshot()
        {
            DateTime? horizon = TimelapseHorizon.Compute(Now, Now.AddMinutes(-30), Lag);

            // Pull minus lag is 11:50, the snapshot sync is 11:30, so the snapshot sync is earlier.
            Assert.Equal(Now.AddMinutes(-30), horizon);
        }

        [Fact]
        public void Compute_SnapshotSyncLater_ReturnsPullMinusLag()
        {
            DateTime? horizon = TimelapseHorizon.Compute(Now, Now.AddMinutes(5), Lag);

            Assert.Equal(Now.AddMinutes(-10), horizon);
        }

        [Fact]
        public void Compute_NoCleanPullYet_ReturnsNull()
        {
            Assert.Null(TimelapseHorizon.Compute(null, Now, Lag));
        }

        [Fact]
        public void Compute_NoSnapshotSyncYet_ReturnsNull()
        {
            Assert.Null(TimelapseHorizon.Compute(Now, null, Lag));
        }

        [Fact]
        public void Compute_NegativeLag_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TimelapseHorizon.Compute(Now, Now, TimeSpan.FromMinutes(-1)));
        }
    }
}
