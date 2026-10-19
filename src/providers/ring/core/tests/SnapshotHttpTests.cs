using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

using Xunit;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Covers the snapshot HTTP plumbing (HttpUtility.DownloadFileWithStatus / DownloadFile and
    /// Session.GetLatestSnapshotWithStatus) with a scripted fake HttpMessageHandler. No test
    /// touches the network.
    /// </summary>
    public class SnapshotHttpTests
    {
        private const string FakeRefreshToken = "fake-refresh-token";
        private const string FakeAccessToken = "fake-access-token";
        private const string SnapshotPath = "/clients_api/snapshots/image/123456";

        private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        /// <summary>Minimal handler that answers each request from a delegate, so tests control status, body and headers.</summary>
        private sealed class ScriptedHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public ScriptedHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responder(request));
            }
        }

        private static HttpResponseMessage ByteResponse(HttpStatusCode status, byte[] body)
        {
            return new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
        }

        private static HttpResponseMessage TokenResponse()
        {
            string json = "{\"access_token\":\"" + FakeAccessToken + "\",\"refresh_token\":\"" + FakeRefreshToken + "\",\"expires_in\":3600,\"token_type\":\"Bearer\"}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        /// <summary>Builds an authenticated Session whose OAuth call is answered by the fake, and whose snapshot calls go to snapshotResponder.</summary>
        private static Task<Session> AuthenticatedSessionAsync(Func<HttpRequestMessage, HttpResponseMessage> snapshotResponder)
        {
            var handler = new ScriptedHttpHandler(request =>
                request.RequestUri!.AbsolutePath.Contains("oauth/token", StringComparison.OrdinalIgnoreCase)
                    ? TokenResponse()
                    : snapshotResponder(request));

            return Session.GetSessionByRefreshToken(FakeRefreshToken, handler);
        }

        private static HttpUtility HttpUtilityWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            return new HttpUtility(messageHandler: new ScriptedHttpHandler(responder));
        }

        private static readonly Uri SnapshotUri = new("https://api.ring.com/clients_api/snapshots/image/123456");

        [Fact]
        public async Task DownloadFileWithStatus_200WithBody_ReturnsStatusAndBytes()
        {
            HttpUtility http = HttpUtilityWith(_ => ByteResponse(HttpStatusCode.OK, JpegBytes));

            HttpUtility.DownloadResult result = await http.DownloadFileWithStatus(SnapshotUri);

            Assert.Equal(HttpStatusCode.OK, result.Status);
            Assert.Equal(JpegBytes, result.Bytes);
            Assert.Null(result.RetryAfter);
        }

        [Fact]
        public async Task DownloadFileWithStatus_429WithRetryAfter_ReturnsStatusAndRetryAfter()
        {
            HttpUtility http = HttpUtilityWith(_ =>
            {
                HttpResponseMessage response = ByteResponse((HttpStatusCode)429, []);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
                return response;
            });

            HttpUtility.DownloadResult result = await http.DownloadFileWithStatus(SnapshotUri);

            Assert.Equal((HttpStatusCode)429, result.Status);
            Assert.Empty(result.Bytes);
            Assert.Equal(TimeSpan.FromSeconds(12), result.RetryAfter);
        }

        [Fact]
        public async Task DownloadFileWithStatus_404_ReturnsStatusWithNoBytes()
        {
            HttpUtility http = HttpUtilityWith(_ => ByteResponse(HttpStatusCode.NotFound, []));

            HttpUtility.DownloadResult result = await http.DownloadFileWithStatus(SnapshotUri);

            Assert.Equal(HttpStatusCode.NotFound, result.Status);
            Assert.Empty(result.Bytes);
        }

        [Fact]
        public async Task DownloadFile_Non200_ReturnsBodyWithoutThrowing()
        {
            // Current behaviour: DownloadFile delegates to DownloadFileWithStatus and returns the body
            // for any status; it does not throw on non-2xx. See the report for the discrepancy with
            // the original expectation.
            HttpUtility http = HttpUtilityWith(_ => ByteResponse(HttpStatusCode.InternalServerError, JpegBytes));

            byte[] bytes = await http.DownloadFile(SnapshotUri);

            Assert.Equal(JpegBytes, bytes);
        }

        [Fact]
        public async Task GetLatestSnapshotWithStatus_200Jpeg_ReturnsJpegBytes()
        {
            string? seenAuthorization = null;
            string? seenPath = null;
            Session session = await AuthenticatedSessionAsync(request =>
            {
                seenPath = request.RequestUri!.AbsolutePath;
                seenAuthorization = request.Headers.Authorization?.ToString();
                return ByteResponse(HttpStatusCode.OK, JpegBytes);
            });

            HttpUtility.DownloadResult result = await session.GetLatestSnapshotWithStatus(123456);

            Assert.Equal(HttpStatusCode.OK, result.Status);
            Assert.Equal(JpegBytes, result.Bytes);
            Assert.Equal(SnapshotPath, seenPath);
            Assert.Equal("Bearer " + FakeAccessToken, seenAuthorization);
        }

        [Fact]
        public async Task GetLatestSnapshotWithStatus_503_ReturnsStatusWithNoBytes()
        {
            Session session = await AuthenticatedSessionAsync(_ => ByteResponse(HttpStatusCode.ServiceUnavailable, []));

            HttpUtility.DownloadResult result = await session.GetLatestSnapshotWithStatus(123456);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, result.Status);
            Assert.Empty(result.Bytes);
        }
    }
}
