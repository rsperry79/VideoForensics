using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class CloudflareTunnelAutostartTests
    {
        [Theory]
        [InlineData(NetworkTierDto.Network, true)]
        [InlineData(NetworkTierDto.Internet, true)]
        public void CloudflareTunnelAutostart_ShouldAutostart_TokenSavedAndNotLocal_Starts(NetworkTierDto tier, bool hasToken)
        {
            Assert.True(CloudflareTunnelAutostart.ShouldAutostart(tier, hasToken));
        }

        [Fact]
        public void CloudflareTunnelAutostart_ShouldAutostart_LocalTier_DoesNotStart()
        {
            Assert.False(CloudflareTunnelAutostart.ShouldAutostart(NetworkTierDto.Local, hasTunnelToken: true));
        }

        [Fact]
        public void CloudflareTunnelAutostart_ShouldAutostart_NoToken_DoesNotStart()
        {
            Assert.False(CloudflareTunnelAutostart.ShouldAutostart(NetworkTierDto.Internet, hasTunnelToken: false));
        }
    }
}
