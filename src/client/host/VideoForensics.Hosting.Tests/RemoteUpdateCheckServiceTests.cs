using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteUpdateCheckServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _factory;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> factory)
            {
                _factory = factory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                CapturedRequest = request;
                return await _factory(request);
            }
        }

        [Fact]
        public async Task TriggerCheckNowAsync_CallsCheckNowEndpoint_UpdatesCachedState()
        {
            // Arrange
            var expectedState = new UpdateCheckStateDto(
                UpdateAvailable: true,
                LatestVersion: "1.2.3",
                CurrentVersion: "1.0.0",
                DownloadUrl: "https://example.com/download.exe",
                LastCheckedUtc: DateTime.UtcNow,
                ErrorMessage: null);

            string json = JsonSerializer.Serialize(expectedState, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteUpdateCheckService(httpClient);

            // Act
            await service.TriggerCheckNowAsync(CancellationToken.None);
            UpdateCheckState cachedState = service.GetState();

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.True(cachedState.UpdateAvailable);
            Assert.Equal("1.2.3", cachedState.LatestVersion);
        }

        [Fact]
        public async Task GetState_AfterTrigger_ReturnsCachedState()
        {
            // Arrange
            var expectedState = new UpdateCheckStateDto(
                UpdateAvailable: false,
                LatestVersion: null,
                CurrentVersion: "1.5.0",
                DownloadUrl: null,
                LastCheckedUtc: DateTime.UtcNow,
                ErrorMessage: null);

            string json = JsonSerializer.Serialize(expectedState, JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteUpdateCheckService(httpClient);

            // Act
            await service.TriggerCheckNowAsync(CancellationToken.None);
            UpdateCheckState state1 = service.GetState();
            UpdateCheckState state2 = service.GetState();

            // Assert - subsequent calls should return the same cached state
            Assert.Equal(state1.CurrentVersion, state2.CurrentVersion);
            Assert.Equal(state1.UpdateAvailable, state2.UpdateAvailable);
            Assert.Equal(state1.LatestVersion, state2.LatestVersion);
        }

        [Fact]
        public void GetState_BeforeAnyTrigger_ReturnsDefaultState()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteUpdateCheckService(httpClient);

            // Act
            UpdateCheckState state = service.GetState();

            // Assert - should return default state before any check
            Assert.False(state.UpdateAvailable);
            Assert.Null(state.LatestVersion);
            Assert.Null(state.LastCheckedUtc);
            Assert.Null(state.ErrorMessage);
        }

        [Fact]
        public async Task TriggerCheckNowAsync_WithErrorResponse_StillCachesState()
        {
            // Arrange - simulate a 500 error
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteUpdateCheckService(httpClient);

            // Act - should not throw even though the response is an error
            await service.TriggerCheckNowAsync(CancellationToken.None);
            UpdateCheckState state = service.GetState();

            // Assert - error should be captured in state
            Assert.NotNull(state.ErrorMessage);
            Assert.NotNull(state.LastCheckedUtc);
            Assert.False(state.UpdateAvailable);
        }

        [Fact]
        public async Task TriggerCheckNowAsync_WithMalformedJson_HandlesGracefully()
        {
            // Arrange - return invalid JSON
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("not valid json", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteUpdateCheckService(httpClient);

            // Act - should not throw on malformed JSON
            await service.TriggerCheckNowAsync(CancellationToken.None);
            UpdateCheckState state = service.GetState();

            // Assert - error should be captured gracefully
            Assert.NotNull(state.ErrorMessage);
            Assert.NotNull(state.LastCheckedUtc);
            Assert.False(state.UpdateAvailable);
        }
    }
}
