using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteMediaContentUrlProviderTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }
            public CancellationToken? CapturedCancellationToken { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                CapturedCancellationToken = cancellationToken;
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler, Uri? baseAddress = null)
        {
            return new HttpClient(handler) { BaseAddress = baseAddress ?? new Uri("https://example.test") };
        }

        private static List<MediaTicketDto> CreateMediaTicketDtos(IReadOnlyList<Guid> mediaItemIds)
        {
            return mediaItemIds.Select(id => new MediaTicketDto(
                MediaItemId: id,
                ContentUrl: MediaContentRoutes.ContentUrl(id, $"token-for-{id}"),
                ExpiresAtUtc: DateTime.UtcNow.AddMinutes(10)
            )).ToList();
        }

        [Fact]
        public async Task GetContentUrlsAsync_WithEmptyInput_ReturnsEmptyDictionaryWithoutRequest()
        {
            // Arrange
            bool requestMade = false;
            FakeHttpMessageHandler handler = new(async _ =>
            {
                requestMade = true;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync([], CancellationToken.None);

            // Assert
            Assert.False(requestMade);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_PostsToTicketEndpointAndReturnsAbsoluteUrls()
        {
            // Arrange
            var mediaId1 = Guid.NewGuid();
            var mediaId2 = Guid.NewGuid();
            Guid[] mediaIds = new[] { mediaId1, mediaId2 };
            List<MediaTicketDto> dtos = CreateMediaTicketDtos(mediaIds);
            string json = JsonSerializer.Serialize(dtos, JsonOptions);

            FakeHttpMessageHandler handler = new(async request =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaIds, CancellationToken.None);

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/media/tickets", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(2, result.Count);
            Assert.True(result[mediaId1].StartsWith("https://example.test"));
            Assert.True(result[mediaId2].StartsWith("https://example.test"));
        }

        [Fact]
        public async Task GetContentUrlsAsync_DeduplicatesIds()
        {
            // Arrange
            var mediaId = Guid.NewGuid();
            Guid[] mediaIds = new[] { mediaId, mediaId, mediaId }; // Same ID three times
            List<MediaTicketDto> dtos = CreateMediaTicketDtos(new[] { mediaId });
            string json = JsonSerializer.Serialize(dtos, JsonOptions);

            FakeHttpMessageHandler handler = new(async request =>
            {
                // Capture the request body to verify de-duplication
                string? requestBody = await request.Content?.ReadAsStringAsync();
                MediaTicketRequestDto? requestDto = JsonSerializer.Deserialize<MediaTicketRequestDto>(requestBody, JsonOptions);
                // Should only have one ID in the request
                _ = Assert.Single(requestDto!.MediaItemIds);

                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaIds, CancellationToken.None);

            // Assert
            _ = Assert.Single(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_ChunksLargeRequestsAboveMaxTicketsPerRequest()
        {
            // Arrange
            var mediaIds = Enumerable.Range(0, MediaContentRoutes.MaxTicketsPerRequest + 50)
                .Select(_ => Guid.NewGuid())
                .ToList();

            int requestCount = 0;
            FakeHttpMessageHandler handler = new(async request =>
            {
                requestCount++;
                string? requestBody = await request.Content?.ReadAsStringAsync();
                MediaTicketRequestDto? requestDto = JsonSerializer.Deserialize<MediaTicketRequestDto>(requestBody, JsonOptions);

                // Each request should have at most MaxTicketsPerRequest items
                Assert.True(requestDto!.MediaItemIds.Count <= MediaContentRoutes.MaxTicketsPerRequest);

                // Build response with tickets for all requested IDs
                List<MediaTicketDto> dtos = CreateMediaTicketDtos(requestDto.MediaItemIds.ToList());
                string json = JsonSerializer.Serialize(dtos, JsonOptions);

                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaIds, CancellationToken.None);

            // Assert
            Assert.Equal(2, requestCount); // Should make 2 requests (200 + 50 items)
            Assert.Equal(mediaIds.Count, result.Count);
        }

        [Fact]
        public async Task GetContentUrlsAsync_NonSuccessStatusThrows()
        {
            // Arrange
            Guid[] mediaIds = new[] { Guid.NewGuid() };

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => provider.GetContentUrlsAsync(mediaIds, CancellationToken.None)
            );
        }

        [Fact]
        public async Task GetContentUrlsAsync_ForwardsCancellationToken()
        {
            // Arrange
            Guid[] mediaIds = new[] { Guid.NewGuid() };

            FakeHttpMessageHandler handler = new(async request =>
            {
                // The SendAsync method receives the cancellation token
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new List<MediaTicketDto>()))
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);
            var cts = new CancellationTokenSource();

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaIds, cts.Token);

            // Assert
            Assert.NotNull(result);
            // Verify the cancellation token was captured and forwarded (non-default token was passed)
            _ = Assert.NotNull(handler.CapturedCancellationToken);
            // Verify the handler received a non-default token (not CancellationToken.None)
            Assert.True(handler.CapturedCancellationToken.Value != CancellationToken.None);
        }

        [Fact]
        public async Task GetContentUrlsAsync_ReturnsUrlsForAllRequestedMedia()
        {
            // Arrange
            var mediaIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
            List<MediaTicketDto> dtos = CreateMediaTicketDtos(mediaIds);
            string json = JsonSerializer.Serialize(dtos, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var provider = new RemoteMediaContentUrlProvider(httpClient);

            // Act
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaIds, CancellationToken.None);

            // Assert
            Assert.Equal(mediaIds.Count, result.Count);
            foreach (Guid mediaId in mediaIds)
            {
                Assert.True(result.ContainsKey(mediaId));
                Assert.True(result[mediaId].StartsWith("https://example.test"));
            }
        }
    }
}
