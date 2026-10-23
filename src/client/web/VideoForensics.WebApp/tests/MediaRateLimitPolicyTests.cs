using Microsoft.AspNetCore.Http;

using Moq;

using System.Threading.RateLimiting;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.RateLimiting;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class MediaRateLimitPolicyTests
    {
        private const string LoopbackIp = "127.0.0.1";

        private static Mock<INetworkTierResolver> CreateResolver(NetworkTier tier)
        {
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(tier);
            _ = resolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns(LoopbackIp);
            return resolver;
        }

        [Fact]
        public void Create_LocalTier_ReturnsUnlimitedPartition()
        {
            Mock<INetworkTierResolver> resolver = CreateResolver(NetworkTier.Local);
            var httpContext = new DefaultHttpContext();

            RateLimitPartition<string> partition = MediaRateLimitPolicy.Create(httpContext, resolver.Object);
            using RateLimiter limiter = partition.Factory(LoopbackIp);

            for (int i = 0; i < 500; i++)
            {
                using RateLimitLease lease = limiter.AttemptAcquire(1);
                Assert.True(lease.IsAcquired, $"Local-tier request {i + 1} was rejected; Local must be unlimited.");
            }
        }

        [Fact]
        public void Create_NetworkTier_LimitsAfter120Requests()
        {
            Mock<INetworkTierResolver> resolver = CreateResolver(NetworkTier.Network);
            var httpContext = new DefaultHttpContext();

            RateLimitPartition<string> partition = MediaRateLimitPolicy.Create(httpContext, resolver.Object);
            using RateLimiter limiter = partition.Factory(LoopbackIp);

            for (int i = 0; i < 120; i++)
            {
                using RateLimitLease lease = limiter.AttemptAcquire(1);
                Assert.True(lease.IsAcquired, $"Network-tier request {i + 1} was rejected before the 120 limit.");
            }

            using RateLimitLease overLimit = limiter.AttemptAcquire(1);
            Assert.False(overLimit.IsAcquired, "Network-tier request 121 must be rejected by the 120/minute limit.");
        }
    }
}
