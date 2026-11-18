using System.Net;

using Microsoft.AspNetCore.Http;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests
{
    public class NetworkTierResolverTests
    {
        private static DefaultHttpContext CreateContext(string remoteIp, string? cfConnectingIp = null)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            if (cfConnectingIp is not null)
            {
                context.Request.Headers["CF-Connecting-IP"] = cfConnectingIp;
            }

            return context;
        }

        [Fact]
        public void ResolveTier_LoopbackWithCfConnectingIp_ReturnsInternet()
        {
            // cloudflared connects to localhost, so tunnel traffic arrives from loopback and
            // carries CF-Connecting-IP. It must not be classed as Local.
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("127.0.0.1", cfConnectingIp: "203.0.113.7");

            Assert.Equal(NetworkTier.Internet, resolver.ResolveTier(context));
        }

        [Fact]
        public void ResolveTier_IPv6LoopbackWithCfConnectingIp_ReturnsInternet()
        {
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("::1", cfConnectingIp: "203.0.113.7");

            Assert.Equal(NetworkTier.Internet, resolver.ResolveTier(context));
        }

        [Fact]
        public void ResolveClientIp_LoopbackTunnelRequest_ReturnsCfConnectingIp()
        {
            // Audit rows and IP bans must name the visitor, not cloudflared's loopback address.
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("127.0.0.1", cfConnectingIp: "203.0.113.7");

            Assert.Equal("203.0.113.7", resolver.ResolveClientIp(context));
        }

        [Fact]
        public void ResolveClientIp_LanPeerWithCfConnectingIp_IgnoresHeader()
        {
            // A LAN peer is not cloudflared, so its CF-Connecting-IP header is spoofable and must be ignored.
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("192.168.1.20", cfConnectingIp: "203.0.113.7");

            Assert.Equal("192.168.1.20", resolver.ResolveClientIp(context));
        }

        [Fact]
        public void ResolveTier_LoopbackWithoutCfConnectingIp_ReturnsLocal()
        {
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("127.0.0.1");

            Assert.Equal(NetworkTier.Local, resolver.ResolveTier(context));
        }

        [Fact]
        public void ResolveTier_CloudflareEdgePeer_ReturnsInternet()
        {
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("173.245.48.10");

            Assert.Equal(NetworkTier.Internet, resolver.ResolveTier(context));
        }

        [Fact]
        public void ResolveTier_LanPeer_ReturnsNetwork()
        {
            var resolver = new NetworkTierResolver();
            HttpContext context = CreateContext("192.168.1.20");

            Assert.Equal(NetworkTier.Network, resolver.ResolveTier(context));
        }
    }
}
