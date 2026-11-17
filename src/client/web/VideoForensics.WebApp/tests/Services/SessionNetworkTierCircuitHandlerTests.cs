using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;

using Moq;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    /// <summary>
    /// A resumed circuit has no prerender, so the tier must be captured when the connection comes up.
    /// </summary>
    public class SessionNetworkTierCircuitHandlerTests
    {
        // Circuit has no public constructor; the handler does not use it.
        private static Circuit NewCircuit() => null!;

        [Fact]
        public async Task OnConnectionUpAsync_HttpContextAvailable_CapturesResolvedTier()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var accessor = new Mock<IHttpContextAccessor>();
            _ = accessor.SetupGet(a => a.HttpContext).Returns(httpContext);
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveTier(httpContext)).Returns(NetworkTier.Local);
            var context = new SessionNetworkContext();
            var handler = new SessionNetworkTierCircuitHandler(accessor.Object, resolver.Object, context);

            // Act
            await handler.OnConnectionUpAsync(NewCircuit(), CancellationToken.None);

            // Assert
            Assert.Equal(NetworkTier.Local, context.Tier);
        }

        [Fact]
        public async Task OnConnectionUpAsync_NoHttpContext_KeepsExistingTier()
        {
            // Arrange
            var accessor = new Mock<IHttpContextAccessor>();
            _ = accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
            var resolver = new Mock<INetworkTierResolver>();
            var context = new SessionNetworkContext();
            context.SetTier(NetworkTier.Local);
            var handler = new SessionNetworkTierCircuitHandler(accessor.Object, resolver.Object, context);

            // Act
            await handler.OnConnectionUpAsync(NewCircuit(), CancellationToken.None);

            // Assert
            Assert.Equal(NetworkTier.Local, context.Tier);
            resolver.Verify(r => r.ResolveTier(It.IsAny<HttpContext>()), Times.Never);
        }
    }
}
