using System.Net;
using System.Net.Http;

using VideoForensics.Providers.Ring.Testing.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Mock-based tests for the clients-API ding recording self-test call (Session.DingRecording.cs).
    /// Verifies request shape only; no live Ring call is made.
    /// </summary>
    public class DingRecordingTests
    {
        private const long DingId = 1234567890123L;
        private const string RecordingBody = @"{ ""url"": ""https://example.invalid/recording.mp4"" }";

        private readonly MockSessionHelper _mockHelper = null!;
        private readonly Session _mockSession = null!;

        public DingRecordingTests()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
        }

        [Fact]
        public async Task GetDingRecordingAsync_RequestsClientsApiDingRecordingPath()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/recording", HttpStatusCode.OK, RecordingBody);
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetDingRecordingAsync(DingId);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.Last(r => r.Url.Contains("/dings/"));
            Assert.Equal($"https://api.ring.com/clients_api/dings/{DingId}/recording", Url);
        }

        [Fact]
        public async Task GetDingRecordingAsync_UsesHttpGet()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/recording", HttpStatusCode.OK, RecordingBody);
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetDingRecordingAsync(DingId);

            (HttpMethod Method, string Url) = mockHandler.RequestLog.Last(r => r.Url.Contains("/dings/"));
            Assert.Equal(HttpMethod.Get, Method);
        }

        [Fact]
        public async Task GetDingRecordingAsync_ReturnsRawResponseBody()
        {
            MockHttpMessageHandler mockHandler = _mockHelper.GetMockHandler();
            mockHandler.SetupResponse($"api.ring.com/clients_api/dings/{DingId}/recording", HttpStatusCode.OK, RecordingBody);
            _ = await _mockSession.Authenticate();

            string result = await _mockSession.GetDingRecordingAsync(DingId);

            Assert.Equal(RecordingBody, result);
        }
    }
}
