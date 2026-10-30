using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests
{
    /// <summary>The sign-in page treats this machine and the private network as local, and sends passkeys to the tunnel from there.</summary>
    public class LocalNetworkAddressTests
    {
        [Theory]
        [InlineData("localhost")]
        [InlineData("127.0.0.1")]
        [InlineData("[::1]")]
        [InlineData("192.168.1.5")]
        [InlineData("10.0.0.7")]
        [InlineData("172.20.1.1")]
        public void LocalNetworkAddress_IsLocalHost_LocalNamesAndPrivateAddresses_IsTrue(string host)
        {
            Assert.True(LocalNetworkAddress.IsLocalHost(host));
        }

        [Theory]
        [InlineData("video.sperryfamily.net")]
        [InlineData("8.8.8.8")]
        [InlineData("172.40.0.1")]
        public void LocalNetworkAddress_IsLocalHost_PublicNamesAndAddresses_IsFalse(string host)
        {
            Assert.False(LocalNetworkAddress.IsLocalHost(host));
        }
    }
}
