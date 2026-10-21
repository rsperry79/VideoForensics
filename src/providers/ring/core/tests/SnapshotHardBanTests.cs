using VideoForensics.Providers.Ring.Exceptions;

using Xunit;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// The hard-ban decision that gates the snapshot calls. Tested as a pure function of the expiry and the clock, so it
    /// never touches the process-wide ban state that other tests in this assembly share.
    /// </summary>
    public class SnapshotHardBanTests
    {
        private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void HardBanFailure_BanRunsPastNow_ReturnsHardBanFailure()
        {
            ThrottledException? failure = HttpUtility.HardBanFailure(Now.AddMinutes(30), Now);

            Assert.NotNull(failure);
            Assert.True(failure!.IsHardBan);
        }

        [Fact]
        public void HardBanFailure_BanHasLifted_ReturnsNull()
        {
            Assert.Null(HttpUtility.HardBanFailure(Now.AddMinutes(-1), Now));
        }

        [Fact]
        public void HardBanFailure_BanEndsExactlyNow_ReturnsNull()
        {
            Assert.Null(HttpUtility.HardBanFailure(Now, Now));
        }

        [Fact]
        public void HardBanFailure_MessageNamesTheLocalTimeTheBanLifts()
        {
            ThrottledException? failure = HttpUtility.HardBanFailure(Now.AddMinutes(30), Now);

            Assert.Contains("Stopping all requests until", failure!.Message);
            Assert.Contains("about 30 more minute(s)", failure.Message);
        }
    }
}
