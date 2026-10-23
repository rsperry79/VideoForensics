using System.Net;

using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// A 429 on the snapshot update call must count as a rate limit, so the account is paused instead of the capture loop
    /// moving on to the next camera at full speed.
    /// </summary>
    public class RingSnapshotRateLimitTests
    {
        [Fact]
        public void AsRateLimit_UpdateReturns429_ReturnsThrottleWith429()
        {
            ThrottledException? throttle = RingMediaDownloadService.AsRateLimit(new UnexpectedOutcomeException(HttpStatusCode.TooManyRequests));

            Assert.NotNull(throttle);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttle!.StatusCode);
        }

        [Fact]
        public void AsRateLimit_UpdateReturns500_ReturnsNull()
        {
            Assert.Null(RingMediaDownloadService.AsRateLimit(new UnexpectedOutcomeException(HttpStatusCode.InternalServerError)));
        }

        [Fact]
        public void AsRateLimit_OtherException_ReturnsNull()
        {
            Assert.Null(RingMediaDownloadService.AsRateLimit(new InvalidOperationException("no session")));
        }
    }
}
