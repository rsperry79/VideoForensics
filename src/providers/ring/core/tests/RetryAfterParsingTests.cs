using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>Reads Retry-After from a 429 response so the media pull can wait what Ring asked for. No network calls.</summary>
    public class RetryAfterParsingTests : IDisposable
    {
        public RetryAfterParsingTests()
        {
            RingTestHardBanIsolation.ResetForTest();
        }

        private static HttpResponseMessage Response(Action<HttpResponseHeaders> addHeaders)
        {
            var response = new HttpResponseMessage((HttpStatusCode)429);
            addHeaders(response.Headers);
            return response;
        }

        [Fact]
        public void ParseRetryAfter_DeltaSeconds_ReturnsThatWait()
        {
            using HttpResponseMessage response = Response(h => h.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12)));

            Assert.Equal(TimeSpan.FromSeconds(12), HttpUtility.ParseRetryAfter(response));
        }

        [Fact]
        public void ParseRetryAfter_HttpDate_ReturnsTimeUntilThatDate()
        {
            DateTimeOffset when = DateTimeOffset.UtcNow.AddSeconds(90);
            using HttpResponseMessage response = Response(h => h.RetryAfter = new RetryConditionHeaderValue(when));

            TimeSpan? wait = HttpUtility.ParseRetryAfter(response);

            _ = Assert.NotNull(wait);
            Assert.InRange(wait!.Value.TotalSeconds, 60, 91);
        }

        [Fact]
        public void ParseRetryAfter_NoHeader_ReturnsNull()
        {
            using HttpResponseMessage response = Response(_ => { });

            Assert.Null(HttpUtility.ParseRetryAfter(response));
        }

        /// <summary>xUnit disposes each test's instance after the test: clear the throttle state it may have set.</summary>
        public void Dispose()
        {
            RingTestHardBanIsolation.ResetForTest();
        }
    }
}
