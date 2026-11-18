using Microsoft.AspNetCore.Http;

using Moq;

using System.Threading.RateLimiting;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.RateLimiting;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.RateLimiting
{
    public class AuthRateLimitPolicyTests
    {
        private const string ClientIp = "203.0.113.7";

        private static Mock<INetworkTierResolver> CreateResolver(NetworkTier tier)
        {
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(tier);
            _ = resolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns(ClientIp);
            return resolver;
        }

        [Fact]
        public void Create_InternetTier_AllowsOnePasskeySignInPerWindow()
        {
            // A passkey sign-in is two requests (options, then complete), so the window must hold at least
            // a few sign-ins, not just two.
            Mock<INetworkTierResolver> resolver = CreateResolver(NetworkTier.Internet);
            var httpContext = new DefaultHttpContext();

            RateLimitPartition<string> partition = AuthRateLimitPolicy.Create(httpContext, resolver.Object);
            using RateLimiter limiter = partition.Factory(ClientIp);

            for (int i = 0; i < AuthRateLimitPolicy.PermitLimit; i++)
            {
                using RateLimitLease lease = limiter.AttemptAcquire(1);
                Assert.True(lease.IsAcquired, $"Auth request {i + 1} was rejected before the limit.");
            }

            using RateLimitLease overLimit = limiter.AttemptAcquire(1);
            Assert.False(overLimit.IsAcquired, "The request after the auth limit must be rejected.");
        }

        [Fact]
        public void Create_InternetTier_PermitLimitCoversSeveralPasskeySignIns()
        {
            Assert.True(AuthRateLimitPolicy.PermitLimit >= 3 * 2,
                "The limit must allow at least three passkey sign-ins (two requests each) per window.");
        }

        [Fact]
        public void Create_LocalTier_ReturnsUnlimitedPartition()
        {
            Mock<INetworkTierResolver> resolver = CreateResolver(NetworkTier.Local);
            var httpContext = new DefaultHttpContext();

            RateLimitPartition<string> partition = AuthRateLimitPolicy.Create(httpContext, resolver.Object);
            using RateLimiter limiter = partition.Factory(ClientIp);

            for (int i = 0; i < 100; i++)
            {
                using RateLimitLease lease = limiter.AttemptAcquire(1);
                Assert.True(lease.IsAcquired, $"Local-tier auth request {i + 1} was rejected; Local must be unlimited.");
            }
        }
    }
}
