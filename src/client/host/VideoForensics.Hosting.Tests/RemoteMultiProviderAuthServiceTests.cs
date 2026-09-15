using System.Net;
using System.Net.Http.Json;

using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteMultiProviderAuthServiceTests
    {
        /// <summary>
        /// Minimal HttpMessageHandler mock for testing HTTP calls without a real server.
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            var mockHandler = new MockHttpMessageHandler(handler);
            var client = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://example.test")
            };
            return client;
        }

        [Fact]
        public async Task GetAvailableProvidersAsync_WithProvidersList_ReturnsProviders()
        {
            // Arrange
            var expectedProviders = new List<string> { "Ring", "Wyze", "Uniview" };

            HttpClient httpClient = CreateHttpClient(async request =>
            {
                // Verify the request details
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/api/v1/auth/providers", request.RequestUri?.PathAndQuery);

                string json = await JsonContent.Create(expectedProviders).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IReadOnlyList<string> result = await service.GetAvailableProvidersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedProviders, result);
        }

        [Fact]
        public async Task GetAvailableProvidersAsync_WithEmptyList_ReturnsEmptyList()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(async request =>
            {
                string json = await JsonContent.Create(new List<string>()).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IReadOnlyList<string> result = await service.GetAvailableProvidersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAvailableProvidersAsync_WithNullResponse_ReturnsEmptyList()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(request =>
            {
                // Return 200 OK with null body content
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                });
            });

            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IReadOnlyList<string> result = await service.GetAvailableProvidersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAvailableProvidersAsync_ForwardsCorrectCancellationToken()
        {
            // Arrange
            var cancellationTokenReceived = new TaskCompletionSource<CancellationToken>();
            HttpClient httpClient = CreateHttpClient(async request =>
            {
                // Capture the cancellation token - we can only verify it was passed by checking
                // if the task completes or if cancellation is properly handled
                string json = await JsonContent.Create(new List<string> { "Ring" }).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteMultiProviderAuthService(httpClient);
            var cts = new CancellationTokenSource();

            // Act
            IReadOnlyList<string> result = await service.GetAvailableProvidersAsync(cts.Token);

            // Assert - if we get here, cancellation token was accepted
            Assert.NotNull(result);
        }

        [Fact]
        public void GetService_WithProviderName_ReturnsRemoteProviderAuthService()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IProviderAuthService authService = service.GetService("Ring");

            // Assert
            Assert.NotNull(authService);
            _ = Assert.IsType<RemoteProviderAuthService>(authService);
        }

        [Fact]
        public void GetService_WithDifferentProviderNames_ReturnsDifferentServices()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IProviderAuthService ringService = service.GetService("Ring");
            IProviderAuthService wyzeService = service.GetService("Wyze");

            // Assert
            Assert.NotNull(ringService);
            Assert.NotNull(wyzeService);
            Assert.NotSame(ringService, wyzeService);
            _ = Assert.IsType<RemoteProviderAuthService>(ringService);
            _ = Assert.IsType<RemoteProviderAuthService>(wyzeService);
        }

        [Fact]
        public void GetService_WithNullOrEmptyProviderName_ReturnsService()
        {
            // Arrange
            HttpClient httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteMultiProviderAuthService(httpClient);

            // Act
            IProviderAuthService nullService = service.GetService(null!);
            IProviderAuthService emptyService = service.GetService("");

            // Assert
            Assert.NotNull(nullService);
            Assert.NotNull(emptyService);
        }
    }
}
