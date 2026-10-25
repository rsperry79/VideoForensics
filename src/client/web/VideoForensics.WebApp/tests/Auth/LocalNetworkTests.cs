using System.Net;

using VideoForensics.Client.WebApp.Auth;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Auth
{
    /// <summary>The local network is this machine and the private address ranges. Internet addresses are not local.</summary>
    public class LocalNetworkTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.1.2.3")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.254")]
        [InlineData("192.168.1.20")]
        [InlineData("169.254.10.10")]
        [InlineData("::1")]
        [InlineData("fd12:3456::1")]
        [InlineData("fe80::1")]
        public void LocalNetwork_IsLocalAddress_LoopbackAndPrivateRanges_IsTrue(string address)
        {
            Assert.True(LocalNetwork.IsLocalAddress(IPAddress.Parse(address)));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("172.32.0.1")]
        [InlineData("192.169.0.1")]
        [InlineData("2606:4700::1")]
        public void LocalNetwork_IsLocalAddress_InternetAddresses_IsFalse(string address)
        {
            Assert.False(LocalNetwork.IsLocalAddress(IPAddress.Parse(address)));
        }

        [Theory]
        [InlineData("localhost")]
        [InlineData("LOCALHOST")]
        [InlineData("192.168.1.5")]
        [InlineData("[::1]")]
        public void LocalNetwork_IsLocalHostName_LocalHostsAndAddresses_IsTrue(string host)
        {
            Assert.True(LocalNetwork.IsLocalHostName(host));
        }

        [Theory]
        [InlineData("video.sperryfamily.net")]
        [InlineData("8.8.8.8")]
        public void LocalNetwork_IsLocalHostName_PublicNamesAndAddresses_IsFalse(string host)
        {
            Assert.False(LocalNetwork.IsLocalHostName(host));
        }
    }
}
