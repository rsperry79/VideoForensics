using System.Net;
using System.Text.Json;
using Moq;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

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
                => _handler(request);
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
            var httpClient = CreateHttpClient(request =>
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
            var httpClient = CreateHttpClient(request =>
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
            var httpClient = CreateHttpClient(request =>
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
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.GetPreScanCounts();

            Assert.Empty(result);
        }

        [Fact]
        public void GetDownloadStatus_ReturnsIdleByDefault()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.GetDownloadStatus();

            Assert.Equal("Idle", result);
        }

        [Fact]
        public void GetRemainingCount_ReturnsZeroByDefault()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.GetRemainingCount();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetCurrentDevice_ReturnsDefaultValues()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var (index, total, name) = service.GetCurrentDevice();

            Assert.Equal(0, index);
            Assert.Equal(0, total);
            Assert.Equal("", name);
        }

        [Fact]
        public void GetProgress_ReturnsDefaultStatus()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.GetProgress();

            Assert.False(result.IsDownloading);
            Assert.Equal(0, result.FilesCompleted);
        }

        [Fact]
        public void DrainActivityLog_ReturnsEmptyByDefault()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.DrainActivityLog();

            Assert.Empty(result);
        }

        [Fact]
        public void GetLastError_ReturnsNull()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            var result = service.GetLastError();

            Assert.Null(result);
        }

        [Fact]
        public async Task AuthenticateAsync_ThrowsNotSupportedException()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = await Assert.ThrowsAsync<NotSupportedException>(() => service.AuthenticateAsync("user", "pass"));
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_ThrowsNotSupportedException()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = Assert.Throws<NotSupportedException>(() => service.GetRateLimitBanUntilUtc());
        }

        [Fact]
        public void OverrideRateLimitBan_ThrowsNotSupportedException()
        {
            var httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var hubConnection = new Mock<ILiveHubConnection>();
            var service = new RemoteVideoDownloadService(httpClient, hubConnection.Object);

            _ = Assert.Throws<NotSupportedException>(() => service.OverrideRateLimitBan());
        }
    }
}
