using System.Net.Http;

using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>Mock-based tests for the app API location history call (rs/history).</summary>
    public class AppHistoryTests
    {
        private readonly MockSessionHelper _mockHelper;
        private readonly Session _mockSession;

        public AppHistoryTests()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper.CreateSessionWithMockHandler();
        }

        [Fact]
        public async Task GetAppLocationHistoryAsync_RequestsAppApiRsHistoryWithAccountId()
        {
            var locationId = Guid.NewGuid();
            MockHttpMessageHandler handler = _mockHelper.GetMockHandler();
            handler.SetupResponse("prd-api-us.prd.rings.solutions/api/v1/rs/history", System.Net.HttpStatusCode.OK, "[]");
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetAppLocationHistoryAsync(locationId);

            (HttpMethod Method, string Url) = handler.RequestLog.Last(r => r.Url.Contains("rs/history"));
            Assert.Equal(HttpMethod.Get, Method);
            Assert.Contains("prd-api-us.prd.rings.solutions/api/v1/rs/history", Url);
            Assert.Contains($"accountId={locationId:D}", Url);
        }

        [Fact]
        public async Task GetAppLocationHistoryAsync_OmitsLimitWhenNotGiven()
        {
            MockHttpMessageHandler handler = _mockHelper.GetMockHandler();
            handler.SetupResponse("rs/history", System.Net.HttpStatusCode.OK, "[]");
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetAppLocationHistoryAsync(Guid.NewGuid());

            Assert.DoesNotContain("limit=", handler.RequestLog.Last(r => r.Url.Contains("rs/history")).Url);
        }

        [Fact]
        public async Task GetAppLocationHistoryAsync_AddsLimitWhenGiven()
        {
            MockHttpMessageHandler handler = _mockHelper.GetMockHandler();
            handler.SetupResponse("rs/history", System.Net.HttpStatusCode.OK, "[]");
            _ = await _mockSession.Authenticate();

            _ = await _mockSession.GetAppLocationHistoryAsync(Guid.NewGuid(), 25);

            Assert.Contains("&limit=25", handler.RequestLog.Last(r => r.Url.Contains("rs/history")).Url);
        }

        [Fact]
        public async Task GetAppLocationHistoryAsync_ReturnsRawResponseBody()
        {
            const string body = "{\"items\":[{\"a\":1}]}";
            MockHttpMessageHandler handler = _mockHelper.GetMockHandler();
            handler.SetupResponse("rs/history", System.Net.HttpStatusCode.OK, body);
            _ = await _mockSession.Authenticate();

            string result = await _mockSession.GetAppLocationHistoryAsync(Guid.NewGuid());

            Assert.Equal(body, result);
        }
    }
}
