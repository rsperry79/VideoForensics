using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

using VideoForensics.Providers.Ring.Exceptions;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Covers the snapshot HTTP plumbing (HttpUtility.DownloadFileWithStatus / DownloadFile and
    /// Session.GetLatestSnapshotWithStatus) with a scripted fake HttpMessageHandler. No test
    /// touches the network.
    /// </summary>
    public class SnapshotHttpTests : IDisposable
    {
        public SnapshotHttpTests()
        {
            RingTestHardBanIsolation.ResetForTest();
        }

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

        [Fact]
        public async Task GetLatestSnapshot_404_ThrowsDownloadFailedWithStatus()
        {
            Session session = await AuthenticatedSessionAsync(_ => ByteResponse(HttpStatusCode.NotFound, Encoding.ASCII.GetBytes("Not Found")));

            DownloadFailedException ex = await Assert.ThrowsAsync<DownloadFailedException>(() => session.GetLatestSnapshot(123456));

            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
            Assert.Equal("Not Found", ex.ResponseBody);
        }

        [Fact]
        public async Task GetLatestSnapshot_SaveAs_404_WritesNoFile()
        {
            Session session = await AuthenticatedSessionAsync(_ => ByteResponse(HttpStatusCode.NotFound, Encoding.ASCII.GetBytes("Not Found")));
            string path = Path.Combine(Path.GetTempPath(), $"snapshot-{Guid.NewGuid():N}.jpg");

            _ = await Assert.ThrowsAsync<DownloadFailedException>(() => session.GetLatestSnapshot(123456, path));

            Assert.False(File.Exists(path), "A non-image response must never be saved as a snapshot file.");
        }

        [Fact]
        public async Task GetLatestSnapshot_SaveAs_200_WritesJpegBytes()
        {
            Session session = await AuthenticatedSessionAsync(_ => ByteResponse(HttpStatusCode.OK, JpegBytes));
            string path = Path.Combine(Path.GetTempPath(), $"snapshot-{Guid.NewGuid():N}.jpg");

            try
            {
                await session.GetLatestSnapshot(123456, path);

                Assert.Equal(JpegBytes, await File.ReadAllBytesAsync(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public async Task GetLatestSnapshot_429_ThrowsThrottledWithRetryAfter()
        {
            Session session = await AuthenticatedSessionAsync(_ =>
            {
                HttpResponseMessage response = ByteResponse((HttpStatusCode)429, []);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
                return response;
            });

            ThrottledException ex = await Assert.ThrowsAsync<ThrottledException>(() => session.GetLatestSnapshot(123456));

            Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
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
        public async Task DownloadFile_200_ReturnsBytes()
        {
            HttpUtility http = HttpUtilityWith(_ => ByteResponse(HttpStatusCode.OK, JpegBytes));

            byte[] bytes = await http.DownloadFile(SnapshotUri);

            Assert.Equal(JpegBytes, bytes);
        }

        [Fact]
        public async Task DownloadFile_Non200_Throws()
        {
            HttpUtility http = HttpUtilityWith(_ => ByteResponse(HttpStatusCode.InternalServerError, JpegBytes));

            DownloadFailedException ex = await Assert.ThrowsAsync<DownloadFailedException>(() => http.DownloadFile(SnapshotUri));

            Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        [Fact]
        public async Task DownloadFile_429_ThrowsThrottledExceptionWithRetryAfter()
        {
            HttpUtility http = HttpUtilityWith(_ =>
            {
                HttpResponseMessage response = ByteResponse((HttpStatusCode)429, []);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
                return response;
            });

            ThrottledException ex = await Assert.ThrowsAsync<ThrottledException>(() => http.DownloadFile(SnapshotUri));

            Assert.Equal(TimeSpan.FromSeconds(12), ex.RetryAfter);
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

        /// <summary>xUnit disposes each test's instance after the test: clear the throttle state it may have set.</summary>
        public void Dispose()
        {
            RingTestHardBanIsolation.ResetForTest();
        }
    }
}
