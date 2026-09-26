using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteMediaItemRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static MediaItemDto CreateMediaItemDto(Guid? mediaItemId = null, Guid? deviceId = null)
        {
            return new MediaItemDto(
                Id: mediaItemId ?? Guid.NewGuid(),
                DeviceId: deviceId ?? Guid.NewGuid(),
                DownloadEventId: Guid.NewGuid(),
                FileName: "2024-01-15_120000.mp4",
                FilePath: "/data/media/2024-01-15_120000.mp4",
                MediaFormat: "mp4",
                FileSizeBytes: 1024 * 1024 * 50, // 50 MB
                RecordedAtUtc: DateTime.UtcNow.AddHours(-1),
                DownloadedAtUtc: DateTime.UtcNow,
                Sha256Hash: "abc123def456",
                VideoCodec: "h264",
                AudioCodec: "aac",
                Resolution: "1920x1080",
                FrameRate: 30.0m,
                IntegrityVerified: true,
                LastVerifiedAtUtc: DateTime.UtcNow,
                IsPurged: false,
                PurgedAtUtc: null,
                PurgeReason: null,
                MetadataJson: null,
                ApiSourceHash: "hash-123"
            );
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        }

        [Fact]
        public async Task ListAsync_CallsGetMediaItemsEndpoint_AndReturnsItems()
        {
            MediaItemDto media1 = CreateMediaItemDto();
            MediaItemDto media2 = CreateMediaItemDto();
            var dtoList = new List<MediaItemDto> { media1, media2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.ListAsync(CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/media-items", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
            Assert.Equal(media1.Id, result[0].Id);
            Assert.Equal(media2.Id, result[1].Id);
        }

        [Fact]
        public async Task ListAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            string json = JsonSerializer.Serialize(new List<MediaItemDto>(), JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async _ =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.ListAsync(cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task ListAsync_EmptyResponse_ReturnsEmptyList()
        {
            string json = JsonSerializer.Serialize(new List<MediaItemDto>(), JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.ListAsync(CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetByDeviceIdAsync_CallsCorrectEndpoint_AndReturnsItems()
        {
            var deviceId = Guid.NewGuid();
            MediaItemDto media1 = CreateMediaItemDto(deviceId: deviceId);
            MediaItemDto media2 = CreateMediaItemDto(deviceId: deviceId);
            var dtoList = new List<MediaItemDto> { media1, media2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.GetByDeviceIdAsync(deviceId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            string? uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Equal($"/api/v1/media-items?deviceId={deviceId}", uri);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetByDeviceIdAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var deviceId = Guid.NewGuid();
            string json = JsonSerializer.Serialize(new List<MediaItemDto>(), JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async _ =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.GetByDeviceIdAsync(deviceId, cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }


        [Fact]
        public async Task GetByHashAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByHashAsync("sha256hash", CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task GetByApiSourceHashAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByApiSourceHashAsync("hash-123", CancellationToken.None));

            Assert.Contains("Remote API doesn't yet support", ex.Message);
        }

        [Fact]
        public async Task GetByDownloadEventIdAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByDownloadEventIdAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("Not supported on a remote", ex.Message);
        }

        [Fact]
        public async Task AddAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            var mediaItem = new MediaItem { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = "test.mp4", FilePath = "/data/test.mp4", MediaFormat = "mp4", Sha256Hash = "hash123" };

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.AddAsync(mediaItem, CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }

        [Fact]
        public async Task UpdateAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            var mediaItem = new MediaItem { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = "test.mp4", FilePath = "/data/test.mp4", MediaFormat = "mp4", Sha256Hash = "hash123" };

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.UpdateAsync(mediaItem, CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }

        [Fact]
        public async Task DeleteAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.DeleteAsync(Guid.NewGuid(), CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }

        [Fact]
        public async Task ListAsync_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var mediaItemDto = new MediaItemDto(
                Id: Guid.NewGuid(),
                DeviceId: Guid.NewGuid(),
                DownloadEventId: Guid.NewGuid(),
                FileName: "2024-01-15_153000.mp4",
                FilePath: "/archive/2024/01/15/153000.mp4",
                MediaFormat: "mp4",
                FileSizeBytes: 1024 * 1024 * 75,
                RecordedAtUtc: new DateTime(2024, 1, 15, 15, 30, 0, DateTimeKind.Utc),
                DownloadedAtUtc: new DateTime(2024, 1, 15, 15, 35, 0, DateTimeKind.Utc),
                Sha256Hash: "hash1234567890abcdef",
                VideoCodec: "h265",
                AudioCodec: "aac",
                Resolution: "2560x1440",
                FrameRate: 60.0m,
                IntegrityVerified: true,
                LastVerifiedAtUtc: new DateTime(2024, 1, 15, 16, 0, 0, DateTimeKind.Utc),
                IsPurged: false,
                PurgedAtUtc: null,
                PurgeReason: null,
                MetadataJson: "{\"codec\":\"h265\"}",
                ApiSourceHash: "api-hash-456"
            );

            string json = JsonSerializer.Serialize(new List<MediaItemDto> { mediaItemDto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.ListAsync(CancellationToken.None);

            MediaItem mediaItem = result[0];
            Assert.Equal(mediaItemDto.Id, mediaItem.Id);
            Assert.Equal(mediaItemDto.DeviceId, mediaItem.DeviceId);
            Assert.Equal(mediaItemDto.DownloadEventId, mediaItem.DownloadEventId);
            Assert.Equal(mediaItemDto.FileName, mediaItem.FileName);
            Assert.Equal(mediaItemDto.FilePath, mediaItem.FilePath);
            Assert.Equal(mediaItemDto.MediaFormat, mediaItem.MediaFormat);
            Assert.Equal(mediaItemDto.FileSizeBytes, mediaItem.FileSizeBytes);
            Assert.Equal(mediaItemDto.RecordedAtUtc, mediaItem.RecordedAtUtc);
            Assert.Equal(mediaItemDto.DownloadedAtUtc, mediaItem.DownloadedAtUtc);
            Assert.Equal(mediaItemDto.Sha256Hash, mediaItem.Sha256Hash);
            Assert.Equal(mediaItemDto.VideoCodec, mediaItem.VideoCodec);
            Assert.Equal(mediaItemDto.AudioCodec, mediaItem.AudioCodec);
            Assert.Equal(mediaItemDto.Resolution, mediaItem.Resolution);
            Assert.Equal(mediaItemDto.FrameRate, mediaItem.FrameRate);
            Assert.Equal(mediaItemDto.IntegrityVerified, mediaItem.IntegrityVerified);
            Assert.Equal(mediaItemDto.LastVerifiedAtUtc, mediaItem.LastVerifiedAtUtc);
            Assert.Equal(mediaItemDto.IsPurged, mediaItem.IsPurged);
            Assert.Equal(mediaItemDto.PurgedAtUtc, mediaItem.PurgedAtUtc);
            Assert.Equal(mediaItemDto.PurgeReason, mediaItem.PurgeReason);
            Assert.Equal(mediaItemDto.MetadataJson, mediaItem.MetadataJson);
            Assert.Equal(mediaItemDto.ApiSourceHash, mediaItem.ApiSourceHash);
        }

        [Fact]
        public async Task GetByDeviceIdAsync_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var deviceId = Guid.NewGuid();
            var mediaItemDto = new MediaItemDto(
                Id: Guid.NewGuid(),
                DeviceId: deviceId,
                DownloadEventId: Guid.NewGuid(),
                FileName: "snapshot.jpg",
                FilePath: "/data/snapshots/snapshot.jpg",
                MediaFormat: "jpeg",
                FileSizeBytes: 1024 * 500,
                RecordedAtUtc: new DateTime(2024, 1, 15, 14, 20, 0, DateTimeKind.Utc),
                DownloadedAtUtc: new DateTime(2024, 1, 15, 14, 21, 0, DateTimeKind.Utc),
                Sha256Hash: "snapshot-hash",
                VideoCodec: null,
                AudioCodec: null,
                Resolution: "1280x720",
                FrameRate: null,
                IntegrityVerified: true,
                LastVerifiedAtUtc: new DateTime(2024, 1, 15, 14, 25, 0, DateTimeKind.Utc),
                IsPurged: false,
                PurgedAtUtc: null,
                PurgeReason: null,
                MetadataJson: null,
                ApiSourceHash: null
            );

            string json = JsonSerializer.Serialize(new List<MediaItemDto> { mediaItemDto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.GetByDeviceIdAsync(deviceId, CancellationToken.None);

            MediaItem mediaItem = result[0];
            Assert.Equal(mediaItemDto.FileName, mediaItem.FileName);
            Assert.Equal(mediaItemDto.FilePath, mediaItem.FilePath);
            Assert.Equal(mediaItemDto.MediaFormat, mediaItem.MediaFormat);
            Assert.Equal(mediaItemDto.FileSizeBytes, mediaItem.FileSizeBytes);
            Assert.Null(mediaItem.VideoCodec);
            Assert.Null(mediaItem.AudioCodec);
            Assert.Equal(mediaItemDto.Resolution, mediaItem.Resolution);
            Assert.Null(mediaItem.FrameRate);
        }

        [Fact]
        public async Task GetByDeviceAndDateRangeAsync_CallsCorrectEndpointWithEscapedQuery_AndReturnsItems()
        {
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            MediaItemDto media1 = CreateMediaItemDto(deviceId: deviceId);
            MediaItemDto media2 = CreateMediaItemDto(deviceId: deviceId);
            var dtoList = new List<MediaItemDto> { media1, media2 };
            string json = JsonSerializer.Serialize(dtoList, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            string? uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);

            // Check that the URI contains the escaped ISO values
            string expectedFromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string expectedToEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            string expectedQuery = $"/api/v1/media-items?deviceId={deviceId}&from={expectedFromEscaped}&to={expectedToEscaped}";
            Assert.Equal(expectedQuery, uri);

            Assert.Equal(2, result.Count);
            Assert.Equal(media1.Id, result[0].Id);
            Assert.Equal(media2.Id, result[1].Id);
        }

        [Fact]
        public async Task GetByDeviceAndDateRangeAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            string json = JsonSerializer.Serialize(new List<MediaItemDto>(), JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async _ =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            IReadOnlyList<MediaItem> result = await repo.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task GetAsync_MediaItemExists_ReturnsItem()
        {
            var mediaItemId = Guid.NewGuid();
            var dto = CreateMediaItemDto(mediaItemId);
            string json = JsonSerializer.Serialize(dto, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            MediaItem? result = await repo.GetAsync(mediaItemId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(mediaItemId, result.Id);
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/media-items/{mediaItemId}", handler.CapturedRequest.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task GetAsync_MediaItemNotFound_ReturnsNull()
        {
            var mediaItemId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            MediaItem? result = await repo.GetAsync(mediaItemId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_ServerError_Throws()
        {
            var mediaItemId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => repo.GetAsync(mediaItemId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var mediaItemId = Guid.NewGuid();
            var dto = CreateMediaItemDto(mediaItemId);
            string json = JsonSerializer.Serialize(dto, JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async _ =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            MediaItem? result = await repo.GetAsync(mediaItemId, cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task GetAsync_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var mediaItemId = Guid.NewGuid();
            var mediaItemDto = new MediaItemDto(
                Id: mediaItemId,
                DeviceId: Guid.NewGuid(),
                DownloadEventId: Guid.NewGuid(),
                FileName: "2024-01-15_153000.mp4",
                FilePath: "/archive/2024/01/15/153000.mp4",
                MediaFormat: "mp4",
                FileSizeBytes: 1024 * 1024 * 75,
                RecordedAtUtc: new DateTime(2024, 1, 15, 15, 30, 0, DateTimeKind.Utc),
                DownloadedAtUtc: new DateTime(2024, 1, 15, 15, 35, 0, DateTimeKind.Utc),
                Sha256Hash: "hash1234567890abcdef",
                VideoCodec: "h265",
                AudioCodec: "aac",
                Resolution: "2560x1440",
                FrameRate: 60.0m,
                IntegrityVerified: true,
                LastVerifiedAtUtc: new DateTime(2024, 1, 15, 16, 0, 0, DateTimeKind.Utc),
                IsPurged: false,
                PurgedAtUtc: null,
                PurgeReason: null,
                MetadataJson: "{\"codec\":\"h265\"}",
                ApiSourceHash: "api-hash-456"
            );

            string json = JsonSerializer.Serialize(mediaItemDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteMediaItemRepository(httpClient);

            MediaItem? result = await repo.GetAsync(mediaItemId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(mediaItemDto.Id, result.Id);
            Assert.Equal(mediaItemDto.DeviceId, result.DeviceId);
            Assert.Equal(mediaItemDto.DownloadEventId, result.DownloadEventId);
            Assert.Equal(mediaItemDto.FileName, result.FileName);
            Assert.Equal(mediaItemDto.FilePath, result.FilePath);
            Assert.Equal(mediaItemDto.MediaFormat, result.MediaFormat);
            Assert.Equal(mediaItemDto.FileSizeBytes, result.FileSizeBytes);
            Assert.Equal(mediaItemDto.RecordedAtUtc, result.RecordedAtUtc);
            Assert.Equal(mediaItemDto.DownloadedAtUtc, result.DownloadedAtUtc);
            Assert.Equal(mediaItemDto.Sha256Hash, result.Sha256Hash);
            Assert.Equal(mediaItemDto.VideoCodec, result.VideoCodec);
            Assert.Equal(mediaItemDto.AudioCodec, result.AudioCodec);
            Assert.Equal(mediaItemDto.Resolution, result.Resolution);
            Assert.Equal(mediaItemDto.FrameRate, result.FrameRate);
            Assert.Equal(mediaItemDto.IntegrityVerified, result.IntegrityVerified);
            Assert.Equal(mediaItemDto.LastVerifiedAtUtc, result.LastVerifiedAtUtc);
            Assert.Equal(mediaItemDto.IsPurged, result.IsPurged);
            Assert.Equal(mediaItemDto.PurgedAtUtc, result.PurgedAtUtc);
            Assert.Equal(mediaItemDto.PurgeReason, result.PurgeReason);
            Assert.Equal(mediaItemDto.MetadataJson, result.MetadataJson);
            Assert.Equal(mediaItemDto.ApiSourceHash, result.ApiSourceHash);
        }
    }
}
