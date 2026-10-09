using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
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

            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

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

            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

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

            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            await service.PreScanAsync("C:\\output", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            Assert.Equal("/api/v1/downloads/pre-scan", capturedRoute);
        }

        [Fact]
        public void GetPreScanCounts_ReturnsEmptyByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            IReadOnlyDictionary<string, int> result = service.GetPreScanCounts();

            Assert.Empty(result);
        }

        [Fact]
        public void GetDownloadStatus_ReturnsIdleByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            string result = service.GetDownloadStatus();

            Assert.Equal("Idle", result);
        }

        [Fact]
        public void GetRemainingCount_ReturnsZeroByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            int result = service.GetRemainingCount();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetCurrentDevice_ReturnsDefaultValues()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            (int index, int total, string? name) = service.GetCurrentDevice();

            Assert.Equal(0, index);
            Assert.Equal(0, total);
            Assert.Equal("", name);
        }

        [Fact]
        public void GetProgress_ReturnsDefaultStatus()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            DownloadStatus result = service.GetProgress();

            Assert.False(result.IsDownloading);
            Assert.Equal(0, result.FilesCompleted);
        }

        [Fact]
        public void DrainActivityLog_ReturnsEmptyByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            IReadOnlyList<string> result = service.DrainActivityLog();

            Assert.Empty(result);
        }

        [Fact]
        public void GetLastError_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            string? result = service.GetLastError();

            Assert.Null(result);
        }

        [Fact]
        public void GetLastError_ReturnsCachedValueFromProgressPush()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var progress = new Subject<DownloadProgressDto>();
            IRealtimeStore store = CreateStore(progress);
            var service = new RemoteVideoDownloadService(httpClient, store);

            progress.OnNext(CreateProgressDto(lastError: "Disk full"));

            Assert.Equal("Disk full", service.GetLastError());
        }

        [Fact]
        public void GetRemainingReason_ReturnsCachedValueFromProgressPush()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var progress = new Subject<DownloadProgressDto>();
            IRealtimeStore store = CreateStore(progress);
            var service = new RemoteVideoDownloadService(httpClient, store);

            progress.OnNext(CreateProgressDto(remainingReason: "Rate limited by provider"));

            Assert.Equal("Rate limited by provider", service.GetRemainingReason());
        }

        [Fact]
        public void GetRemainingReason_ReturnsNullByDefault()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            Assert.Null(service.GetRemainingReason());
        }

        /// <summary>
        /// Real <see cref="RealtimeStore"/> over a fake hub whose DownloadProgress stream is <paramref name="progress"/>
        /// (a fresh, silent subject by default). Tests push values through the subject, as the hub would.
        /// </summary>
        private static IRealtimeStore CreateStore(Subject<DownloadProgressDto>? progress = null)
        {
            return new RealtimeStore(new FakeRealtimeHub(progress ?? new Subject<DownloadProgressDto>()));
        }

        /// <summary>Hub double that counts DownloadProgress subscriptions, so tests can prove per-instance subscribing is gone.</summary>
        private sealed class FakeRealtimeHub : IRealtimeHub
        {
            private readonly Subject<DownloadProgressDto> _progress;

            public FakeRealtimeHub(Subject<DownloadProgressDto> progress)
            {
                _progress = progress;
            }

            public int DownloadProgressSubscriptions { get; private set; }

            public IObservable<DownloadProgressDto> DownloadProgress => Observable.Create<DownloadProgressDto>(observer =>
            {
                DownloadProgressSubscriptions++;
                return _progress.Subscribe(observer);
            });

            public IObservable<NotificationEvent> UrgentEvents { get; } = new Subject<NotificationEvent>();
            public IObservable<SelfTestStatusDto> SelfTestStatus { get; } = new Subject<SelfTestStatusDto>();
            public IObservable<ConnectionState> Connection { get; } = new Subject<ConnectionState>();
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;
        }

        private static DownloadProgressDto CreateProgressDto(string? lastError = null, string? remainingReason = null, IReadOnlyList<string>? activity = null)
        {
            return new DownloadProgressDto(
                new DownloadStatusDto(false, 0, 0, 0),
                0,
                0,
                null,
                activity ?? Array.Empty<string>(),
                new Dictionary<string, int>(),
                lastError,
                remainingReason);
        }

        [Fact]
        public void RemoteVideoDownloadService_ManyInstancesOverOneStore_AddNoHubSubscriptions()
        {
            var progress = new Subject<DownloadProgressDto>();
            var hub = new FakeRealtimeHub(progress);
            IRealtimeStore store = new RealtimeStore(hub);
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            for (int i = 0; i < 10; i++)
            {
                _ = new RemoteVideoDownloadService(httpClient, store);
            }

            // Only the store subscribes to the hub. Transient typed clients must not add subscriptions.
            Assert.Equal(1, hub.DownloadProgressSubscriptions);
        }

        [Fact]
        public void RemoteVideoDownloadService_GetProgress_ReflectsLatestStoreValueAfterEmission()
        {
            var progress = new Subject<DownloadProgressDto>();
            IRealtimeStore store = CreateStore(progress);
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var service = new RemoteVideoDownloadService(httpClient, store);

            progress.OnNext(new DownloadProgressDto(new DownloadStatusDto(true, 4, 10, 0, TotalFilesCompleted: 4, TotalFilesMatched: 10), 2, 3, "Front door",
                Array.Empty<string>(), new Dictionary<string, int>(), null, null));

            DownloadStatus result = service.GetProgress();
            Assert.True(result.IsDownloading);
            Assert.Equal(4, result.FilesCompleted);
            Assert.Equal(6, service.GetRemainingCount());
            Assert.Equal("Downloading media for device 2 of 3", service.GetDownloadStatus());
            Assert.Equal("Front door", service.GetCurrentDevice().Name);
        }

        [Fact]
        public void DrainActivityLog_AfterEmissionsWithActivity_ReturnsLinesFromStoreAndClears()
        {
            var progress = new Subject<DownloadProgressDto>();
            IRealtimeStore store = CreateStore(progress);
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            var service = new RemoteVideoDownloadService(httpClient, store);

            progress.OnNext(CreateProgressDto(activity: new[] { "Downloading clip 1" }));
            progress.OnNext(CreateProgressDto(activity: new[] { "Downloading clip 2", "Done" }));

            Assert.Equal(new[] { "Downloading clip 1", "Downloading clip 2", "Done" }, service.DrainActivityLog());
            Assert.Empty(service.DrainActivityLog());
        }

        [Fact]
        public async Task AuthenticateAsync_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            _ = await Assert.ThrowsAsync<NotSupportedException>(() => service.AuthenticateAsync("user", "pass"));
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            _ = Assert.Throws<NotSupportedException>(() => service.GetRateLimitBanUntilUtc());
        }

        [Fact]
        public void OverrideRateLimitBan_ThrowsNotSupportedException()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            IRealtimeStore store = CreateStore();
            var service = new RemoteVideoDownloadService(httpClient, store);

            _ = Assert.Throws<NotSupportedException>(service.OverrideRateLimitBan);
        }
    }
}
