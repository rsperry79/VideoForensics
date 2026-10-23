using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// <see cref="RequestTierResolver"/> is the single place both an ALREADY-authenticated request's
    /// tier claim (<see cref="PairedDeviceAuthenticationHandler"/>) and a PRE-AUTH endpoint's own
    /// tier-gated decision (e.g. <c>OperatorAuthEndpoints.LoginPasswordAsync</c>'s primary-SuperAdmin
    /// Local-only check) resolve the calling circuit's REAL network tier from the WebApp's own
    /// self-HTTP <c>X-VF-Session-Tier</c> header, rather than trusting a self-call's own (always
    /// loopback) connection.
    /// </summary>
    public class RequestTierResolverTests
    {
        private static HttpContext CreateContext(NetworkTier connectionTier, string? headerValue = null)
        {
            var context = new DefaultHttpContext();
            if (headerValue != null)
            {
                context.Request.Headers[SessionTierHeaderNames.HeaderName] = headerValue;
            }

            return context;
        }

        private static Mock<INetworkTierResolver> MockTierResolver(NetworkTier tier)
        {
            var mock = new Mock<INetworkTierResolver>();
            mock.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(tier);
            return mock;
        }

        // --- ResolvePreAuth (no operator identity yet) ---

        [Fact]
        public void ResolvePreAuth_NoHeader_ReturnsConnectionTierUnchanged()
        {
            HttpContext context = CreateContext(NetworkTier.Local);
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Local, result);
            headerProtector.Verify(p => p.TryUnprotect(It.IsAny<string>(), out It.Ref<NetworkTier>.IsAny, out It.Ref<Guid?>.IsAny), Times.Never);
        }

        [Fact]
        public void ResolvePreAuth_ValidPreAuthHeaderOnLoopback_ReturnsHeaderTier()
        {
            // A remote browser's real tier (captured by SessionNetworkContext/NetworkTierCapture.razor)
            // travels as an Internet-tier pre-auth header on the self-call's own loopback connection.
            HttpContext context = CreateContext(NetworkTier.Local, "pre-auth-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Internet;
            Guid? outOperatorId = null;
            headerProtector.Setup(p => p.TryUnprotect("pre-auth-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Internet, result);
        }

        [Fact]
        public void ResolvePreAuth_ValidLocalPreAuthHeaderOnLoopback_ReturnsLocal()
        {
            HttpContext context = CreateContext(NetworkTier.Local, "pre-auth-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid? outOperatorId = null;
            headerProtector.Setup(p => p.TryUnprotect("pre-auth-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Local, result);
        }

        [Fact]
        public void ResolvePreAuth_ValidOperatorBoundHeaderOnLoopback_AlsoAccepted()
        {
            // An operator-bound header (stronger evidence than pre-auth) is also acceptable for a
            // pre-auth decision - there's simply no operator identity yet to check it against.
            HttpContext context = CreateContext(NetworkTier.Local, "operator-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid? outOperatorId = Guid.NewGuid();
            headerProtector.Setup(p => p.TryUnprotect("operator-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Local, result);
        }

        [Fact]
        public void ResolvePreAuth_TamperedOrExpiredHeaderOnLoopback_FailsSafeToInternet()
        {
            HttpContext context = CreateContext(NetworkTier.Local, "bad-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = default;
            Guid? outOperatorId = null;
            headerProtector.Setup(p => p.TryUnprotect("bad-header", out outTier, out outOperatorId)).Returns(false);

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Internet, result);
        }

        [Fact]
        public void ResolvePreAuth_HeaderOnNonLoopbackConnection_IgnoredResolvesByIp()
        {
            // A genuinely remote caller (real connection tier != Local) cannot use the header to
            // claim a better tier than their real one.
            HttpContext context = CreateContext(NetworkTier.Network, "claims-local");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Network);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid? outOperatorId = null;
            headerProtector.Setup(p => p.TryUnprotect(It.IsAny<string>(), out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolvePreAuth(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance);

            Assert.Equal(NetworkTier.Network, result);
            headerProtector.Verify(p => p.TryUnprotect(It.IsAny<string>(), out outTier, out outOperatorId), Times.Never);
        }

        // --- ResolveForOperator (already-authenticated) ---

        [Fact]
        public void ResolveForOperator_MatchingOperatorBoundHeaderOnLoopback_ReturnsHeaderTier()
        {
            Guid operatorId = Guid.NewGuid();
            HttpContext context = CreateContext(NetworkTier.Local, "operator-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Internet;
            Guid? outOperatorId = operatorId;
            headerProtector.Setup(p => p.TryUnprotect("operator-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolveForOperator(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance, operatorId);

            Assert.Equal(NetworkTier.Internet, result);
        }

        [Fact]
        public void ResolveForOperator_PreAuthHeaderOnAuthenticatedRequest_FailsSafeToInternet()
        {
            // A pre-auth header (no operator bound at all) must never be honored on an ALREADY
            // authenticated request - it isn't bound to this (or any) operator, so it can't be trusted
            // to carry that operator's real tier.
            Guid operatorId = Guid.NewGuid();
            HttpContext context = CreateContext(NetworkTier.Local, "pre-auth-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid? outOperatorId = null; // pre-auth payload: no operator bound
            headerProtector.Setup(p => p.TryUnprotect("pre-auth-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolveForOperator(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance, operatorId);

            Assert.Equal(NetworkTier.Internet, result);
        }

        [Fact]
        public void ResolveForOperator_MismatchedOperatorHeaderOnLoopback_FailsSafeToInternet()
        {
            HttpContext context = CreateContext(NetworkTier.Local, "operator-header");
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();
            NetworkTier outTier = NetworkTier.Local;
            Guid? outOperatorId = Guid.NewGuid();
            headerProtector.Setup(p => p.TryUnprotect("operator-header", out outTier, out outOperatorId)).Returns(true);

            NetworkTier result = RequestTierResolver.ResolveForOperator(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance, Guid.NewGuid());

            Assert.Equal(NetworkTier.Internet, result);
        }

        [Fact]
        public void ResolveForOperator_NoHeader_ReturnsConnectionTierUnchanged()
        {
            HttpContext context = CreateContext(NetworkTier.Local);
            Mock<INetworkTierResolver> tierResolver = MockTierResolver(NetworkTier.Local);
            var headerProtector = new Mock<ISessionTierHeaderProtector>();

            NetworkTier result = RequestTierResolver.ResolveForOperator(context, tierResolver.Object, headerProtector.Object, NullLogger.Instance, Guid.NewGuid());

            Assert.Equal(NetworkTier.Local, result);
        }
    }
}
