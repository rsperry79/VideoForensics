using System.Net;
using System.Net.Http;

using VideoForensics.Providers.Ring.Testing.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Mock-based tests for the clients-API ding share/play self-test call (Session.DingSharePlay.cs).
    /// Verifies request shape only; no live Ring call is made.
    /// </summary>
    public class DingSharePlayTests
    {
        private const long DingId = 1234567890123L;
        private const string SharePlayBody = @"{ ""url"": ""https://example.invalid/share-play.mp4"" }";

        private readonly MockSessionHelper _mockHelper = null!;
        private readonly Session _mockSession = null!;

        public DingSharePlayTests()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
        }

        [Fact]
        public async Task GetDingSharePlayAsync_RequestsClientsApiDingSharePlayPath()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/share/play", HttpStatusCode.OK, SharePlayBody);
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetDingSharePlayAsync(DingId);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.Last(r => r.Url.Contains("/dings/"));
            Assert.Equal($"https://api.ring.com/clients_api/dings/{DingId}/share/play?disable_redirect=true", Url);
        }

        [Fact]
        public async Task GetDingSharePlayAsync_UsesHttpGet()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/share/play", HttpStatusCode.OK, SharePlayBody);
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetDingSharePlayAsync(DingId);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.Last(r => r.Url.Contains("/dings/"));
            Assert.Equal(HttpMethod.Get, Method);
        }

        [Fact]
        public async Task GetDingSharePlayAsync_ReturnsRawResponseBody()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/share/play", HttpStatusCode.OK, SharePlayBody);
            _ = await _mockSession.Authenticate();

            string result = await _mockSession.GetDingSharePlayAsync(DingId);

            Assert.Equal(SharePlayBody, result);
        }
    }
}
