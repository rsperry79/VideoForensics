using Moq;

using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteVideoDownloadServiceTests
    {
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        private HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            return new HttpClient(new TestHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("https://example.test")
            };
        }

        [Fact]
        public async Task DownloadVideosAsync_CallsCorrectRoute()
        {
            string capturedRoute = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new DownloadOperationResponseDto(true, "Queued")))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = await service.DownloadVideosAsync("C:\\output", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            Assert.Equal("/api/v1/downloads/videos", capturedRoute);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_CallsCorrectRoute()
        {
            string capturedRoute = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new DownloadOperationResponseDto(true, "Queued")))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = await service.DownloadSnapshotsAsync("C:\\output", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            Assert.Equal("/api/v1/downloads/snapshots", capturedRoute);
        }

        [Fact]
        public async Task PreScanAsync_CallsCorrectRoute()
        {
            string capturedRoute = "";
            HttpClient httpClient = CreateHttpClient(request =>
            {
                capturedRoute = request.RequestUri?.PathAndQuery ?? "";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            });

            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            await service.PreScanAsync("C:\\output", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            Assert.Equal("/api/v1/downloads/pre-scan", capturedRoute);
        }

        [Fact]
        public void GetPreScanCounts_ReturnsEmptyByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            IReadOnlyDictionary<string, int> result = service.GetPreScanCounts();

            Assert.Empty(result);
        }

        [Fact]
        public void GetDownloadStatus_ReturnsIdleByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            string result = service.GetDownloadStatus();

            Assert.Equal("Idle", result);
        }

        [Fact]
        public void GetRemainingCount_ReturnsZeroByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            int result = service.GetRemainingCount();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetCurrentDevice_ReturnsDefaultValues()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            (int index, int total, string? name) = service.GetCurrentDevice();

            Assert.Equal(0, index);
            Assert.Equal(0, total);
            Assert.Equal("", name);
        }

        [Fact]
        public void GetProgress_ReturnsDefaultStatus()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            DownloadStatus result = service.GetProgress();

            Assert.False(result.IsDownloading);
            Assert.Equal(0, result.FilesCompleted);
        }

        [Fact]
        public void DrainActivityLog_ReturnsEmptyByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            IReadOnlyList<string> result = service.DrainActivityLog();

            Assert.Empty(result);
        }

        [Fact]
        public void GetLastError_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            string? result = service.GetLastError();

            Assert.Null(result);
        }

        [Fact]
        public async Task AuthenticateAsync_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = await Assert.ThrowsAsync<NotSupportedException>(() => service.AuthenticateAsync("user", "pass"));
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = Assert.Throws<NotSupportedException>(() => service.GetRateLimitBanUntilUtc());
        }

        [Fact]
        public void OverrideRateLimitBan_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = Assert.Throws<NotSupportedException>(service.OverrideRateLimitBan);
        }
    }
}
