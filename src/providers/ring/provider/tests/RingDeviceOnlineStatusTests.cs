using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Ring's offline rule: a device is offline only when Ring reports it disconnected.</summary>
    public class RingDeviceOnlineStatusTests
    {
        private readonly IProviderDeviceStatus _status = new RingDeviceOnlineStatus();

        [Theory]
        [InlineData("doorbot")]
        [InlineData("stickup_cam")]
        [InlineData("chime")]
        public void IsOffline_ReportedDisconnected_ReturnsTrue(string deviceType)
        {
            Assert.True(_status.IsOffline(deviceType, isOnline: false));
        }

        [Theory]
        [InlineData("doorbot")]
        [InlineData("chime_pro")]
        public void IsOffline_ReportedConnected_ReturnsFalse(string deviceType)
        {
            Assert.False(_status.IsOffline(deviceType, isOnline: true));
        }

        [Fact]
        public void IsOffline_UnknownConnectionState_ReturnsFalse()
        {
            // Null means Ring gave no connection state; it is not evidence of an outage.
            Assert.False(_status.IsOffline("doorbot", isOnline: null));
        }
    }
}
