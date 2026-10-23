using Microsoft.AspNetCore.Http;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    /// <summary>
    /// SessionNetworkContext is the circuit-scoped record of a Blazor Server session's REAL network
    /// tier - resolved once from the browser's actual initial connection (see
    /// Components/NetworkTierCapture.razor) and then carried, via the session-tier header, into
    /// every self-HTTP call this circuit makes back into its own Minimal API (see
    /// SelfHttpServiceExtensions/SessionTierHeaderHandler).
    /// </summary>
    public class SessionNetworkContextTests
    {
        [Fact]
        public void Tier_NeverSet_DefaultsToInternet()
        {
            // Arrange
            var context = new SessionNetworkContext();

            // Act & Assert - unknown/undetermined tier must fail safe to the MOST restrictive tier.
            Assert.Equal(NetworkTier.Internet, context.Tier);
        }

        [Fact]
        public void CaptureFromInitialConnection_LoopbackHttpContext_CapturesLocal()
        {
            // Arrange
            var context = new SessionNetworkContext();
            var httpContext = new DefaultHttpContext();
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveTier(httpContext)).Returns(NetworkTier.Local);

            // Act
            context.CaptureFromInitialConnection(httpContext, resolver.Object);

            // Assert
            Assert.Equal(NetworkTier.Local, context.Tier);
        }

        [Fact]
        public void CaptureFromInitialConnection_NullHttpContext_DefaultsToInternet()
        {
            // Arrange - no HttpContext to resolve from at all (e.g. a render mode with no
            // prerender pass) - the tier could not be determined, so it must fail safe.
            var context = new SessionNetworkContext();
            var resolver = new Mock<INetworkTierResolver>();

            // Act
            context.CaptureFromInitialConnection(null, resolver.Object);

            // Assert
            Assert.Equal(NetworkTier.Internet, context.Tier);
            resolver.Verify(r => r.ResolveTier(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public void CaptureFromInitialConnection_HttpContext_RecordsClientIpFromResolver()
        {
            var context = new SessionNetworkContext();
            var httpContext = new DefaultHttpContext();
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveClientIp(httpContext)).Returns("203.0.113.7");

            context.CaptureFromInitialConnection(httpContext, resolver.Object);

            Assert.Equal("203.0.113.7", context.ClientIp);
        }

        [Fact]
        public void CaptureFromInitialConnection_NullHttpContext_LeavesClientIpNull()
        {
            var context = new SessionNetworkContext();
            var resolver = new Mock<INetworkTierResolver>();

            context.CaptureFromInitialConnection(null, resolver.Object);

            Assert.Null(context.ClientIp);
        }

        [Fact]
        public void CaptureFromInitialConnection_SecondCapture_KeepsFirstClientIp()
        {
            var context = new SessionNetworkContext();
            var first = new DefaultHttpContext();
            var second = new DefaultHttpContext();
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveClientIp(first)).Returns("203.0.113.7");
            _ = resolver.Setup(r => r.ResolveClientIp(second)).Returns("198.51.100.9");

            context.CaptureFromInitialConnection(first, resolver.Object);
            context.CaptureFromInitialConnection(second, resolver.Object);

            Assert.Equal("203.0.113.7", context.ClientIp);
        }

        [Fact]
        public void SetTier_AfterCapture_DoesNotChangeClientIp()
        {
            var context = new SessionNetworkContext();
            var httpContext = new DefaultHttpContext();
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveClientIp(httpContext)).Returns("203.0.113.7");
            context.CaptureFromInitialConnection(httpContext, resolver.Object);

            context.SetTier(NetworkTier.Internet);

            Assert.Equal("203.0.113.7", context.ClientIp);
        }

        [Fact]
        public void SetTier_MoreRestrictiveThanCurrent_Updates()
        {
            // Arrange
            var context = new SessionNetworkContext();
            context.SetTier(NetworkTier.Local);

            // Act
            context.SetTier(NetworkTier.Internet);

            // Assert
            Assert.Equal(NetworkTier.Internet, context.Tier);
        }

        [Fact]
        public void SetTier_LessRestrictiveThanCurrent_NeverLoosens()
        {
            // Arrange - simulates a reconnect re-resolving a weaker tier than what was already
            // recorded; the ORIGINAL, more restrictive tier must be kept.
            var context = new SessionNetworkContext();
            context.SetTier(NetworkTier.Internet);

            // Act
            context.SetTier(NetworkTier.Local);

            // Assert
            Assert.Equal(NetworkTier.Internet, context.Tier);
        }

        [Fact]
        public void SetTier_SameTierAgain_StaysSet()
        {
            // Arrange
            var context = new SessionNetworkContext();
            context.SetTier(NetworkTier.Network);

            // Act
            context.SetTier(NetworkTier.Network);

            // Assert
            Assert.Equal(NetworkTier.Network, context.Tier);
        }
    }
}
