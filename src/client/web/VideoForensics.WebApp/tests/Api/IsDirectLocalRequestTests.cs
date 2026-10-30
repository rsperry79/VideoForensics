using Microsoft.AspNetCore.Http;

using System.Net;

using VideoForensics.Client.WebApp.Api;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class IsDirectLocalRequestTests
    {
        private static HttpContext Request(string host, IPAddress? remote, string? cfConnectingIp = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            context.Connection.RemoteIpAddress = remote;
            if (cfConnectingIp is not null)
            {
                context.Request.Headers["CF-Connecting-IP"] = cfConnectingIp;
            }

            return context;
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_LocalhostFromLoopback_IsTrue()
        {
            Assert.True(OperatorAuthEndpoints.IsDirectLocalRequest(Request("localhost:5162", IPAddress.Loopback)));
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_TunnelHostFromLoopback_IsFalse()
        {
            // cloudflared connects to localhost, so tunnel traffic looks loopback except for the public Host.
            Assert.False(OperatorAuthEndpoints.IsDirectLocalRequest(Request("video.sperryfamily.net", IPAddress.Loopback)));
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_LanAddressFromLanClient_IsTrue()
        {
            // A device on the local network reaches the server by its LAN address, which counts as local.
            Assert.True(OperatorAuthEndpoints.IsDirectLocalRequest(Request("192.168.1.5:5000", System.Net.IPAddress.Parse("192.168.1.20"))));
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_InternetAddressFromInternetClient_IsFalse()
        {
            Assert.False(OperatorAuthEndpoints.IsDirectLocalRequest(Request("8.8.8.8:5000", System.Net.IPAddress.Parse("8.8.4.4"))));
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_CloudflareHeaderPresent_IsFalse()
        {
            Assert.False(OperatorAuthEndpoints.IsDirectLocalRequest(Request("localhost:5162", IPAddress.Loopback, "203.0.113.9")));
        }

        [Fact]
        public void OperatorAuthEndpoints_IsDirectLocalRequest_LanAddressFromLanHost_IsTrue()
        {
            Assert.True(OperatorAuthEndpoints.IsDirectLocalRequest(Request("192.168.1.20:5162", IPAddress.Parse("192.168.1.55"))));
        }
    }
}
