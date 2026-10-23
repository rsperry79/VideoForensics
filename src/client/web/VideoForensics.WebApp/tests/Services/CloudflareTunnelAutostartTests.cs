using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class CloudflareTunnelAutostartTests
    {
        [Theory]
        [InlineData(NetworkTier.Network, true)]
        [InlineData(NetworkTier.Internet, true)]
        public void CloudflareTunnelAutostart_ShouldAutostart_TokenSavedAndNotLocal_Starts(NetworkTier tier, bool hasToken)
        {
            Assert.True(CloudflareTunnelAutostart.ShouldAutostart(tier, hasToken));
        }

        [Fact]
        public void CloudflareTunnelAutostart_ShouldAutostart_LocalTier_DoesNotStart()
        {
            Assert.False(CloudflareTunnelAutostart.ShouldAutostart(NetworkTier.Local, hasTunnelToken: true));
        }

        [Fact]
        public void CloudflareTunnelAutostart_ShouldAutostart_NoToken_DoesNotStart()
        {
            Assert.False(CloudflareTunnelAutostart.ShouldAutostart(NetworkTier.Internet, hasTunnelToken: false));
        }
    }
}
