using System.Net;

using Microsoft.AspNetCore.SignalR.Client;

using VideoForensics.Hosting;

namespace VideoForensics.Hosting.Tests
{
    public class CappedBackoffRetryPolicyTests
    {
        private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void CappedBackoffRetryPolicy_PastDefaultLimit_KeepsRetrying(int previousRetryCount)
        {
            var policy = new CappedBackoffRetryPolicy(BaseDelay, MaxDelay);

            TimeSpan? delay = policy.NextRetryDelay(CreateContext(previousRetryCount));

            Assert.NotNull(delay);
            Assert.True(delay <= MaxDelay);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(5, 30)]
        [InlineData(50, 30)]
        public void CappedBackoffRetryPolicy_Attempt_DoublesThenCapsAtMax(int previousRetryCount, int expectedSeconds)
        {
            var policy = new CappedBackoffRetryPolicy(BaseDelay, MaxDelay);

            TimeSpan? delay = policy.NextRetryDelay(CreateContext(previousRetryCount));

            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
        }

        [Fact]
        public void CappedBackoffRetryPolicy_Unauthorized_ReturnsNullToStopRetrying()
        {
            var policy = new CappedBackoffRetryPolicy(BaseDelay, MaxDelay);
            var unauthorized = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

            TimeSpan? delay = policy.NextRetryDelay(CreateContext(0, unauthorized));

            Assert.Null(delay);
        }

        private static RetryContext CreateContext(int previousRetryCount, Exception? reason = null)
        {
            return new RetryContext
            {
                PreviousRetryCount = previousRetryCount,
                ElapsedTime = TimeSpan.Zero,
                RetryReason = reason ?? new HttpRequestException("connection lost")
            };
        }
    }
}
