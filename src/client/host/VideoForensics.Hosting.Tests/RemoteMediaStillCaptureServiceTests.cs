using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteMediaStillCaptureServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }
            public string? CapturedBody { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                if (request.Content is not null)
                {
                    CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
                }
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        }

        private static MediaStillDto CreateDto(Guid? id = null, Guid? sourceId = null)
        {
            return new MediaStillDto(
                Id: id ?? Guid.NewGuid(),
                SourceMediaItemId: sourceId ?? Guid.NewGuid(),
                FrameOffsetMs: 1500,
                Sha256Hash: "still-hash",
                SourceSha256AtCapture: "source-hash",
                CapturedByOperator: "operator-1",
                CaptureMethod: "Server",
                CreatedAtUtc: DateTime.UtcNow,
                PinnedToCase: false);
        }

        [Fact]
        public async Task CaptureStillAsync_Success_ReturnsOkResultWithMappedInfo()
        {
            var sourceId = Guid.NewGuid();
            var dto = CreateDto(sourceId: sourceId);
            string json = JsonSerializer.Serialize(dto, JsonOptions);

            var handler = new FakeHttpMessageHandler(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
            var service = new RemoteMediaStillCaptureService(CreateHttpClientWithHandler(handler));

            MediaStillCaptureResult result = await service.CaptureStillAsync(
                sourceId, 1500, null, null, null, "operator-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(dto.Id, result.Still!.Id);
            Assert.Equal("still-hash", result.Still.Sha256Hash);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest!.Method);
            Assert.Equal($"/api/v1/media/{sourceId}/stills", handler.CapturedRequest.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task CaptureStillAsync_SendsRequestBodyWithCaseAndReason()
        {
            var sourceId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var dto = CreateDto(sourceId: sourceId);
            string json = JsonSerializer.Serialize(dto, JsonOptions);

            var handler = new FakeHttpMessageHandler(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
            var service = new RemoteMediaStillCaptureService(CreateHttpClientWithHandler(handler));

            _ = await service.CaptureStillAsync(sourceId, 2000, "cGluZw==", caseId, "reason text", "operator-1", CancellationToken.None);

            Assert.NotNull(handler.CapturedBody);
            CaptureStillRequestDto? sentRequest = JsonSerializer.Deserialize<CaptureStillRequestDto>(handler.CapturedBody!, JsonOptions);
            Assert.NotNull(sentRequest);
            Assert.Equal(2000, sentRequest!.FrameOffsetMs);
            Assert.Equal("cGluZw==", sentRequest.PngBase64);
            Assert.Equal(caseId, sentRequest.CaseId);
            Assert.Equal("reason text", sentRequest.Reason);
        }

        [Fact]
        public async Task CaptureStillAsync_NotFound_ReturnsMediaNotFoundError()
        {
            var handler = new FakeHttpMessageHandler(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var service = new RemoteMediaStillCaptureService(CreateHttpClientWithHandler(handler));

            MediaStillCaptureResult result = await service.CaptureStillAsync(
                Guid.NewGuid(), 100, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.MediaNotFound, result.Error);
        }

        [Fact]
        public async Task CaptureStillAsync_BadRequest_ReturnsInvalidInputErrorWithBody()
        {
            var handler = new FakeHttpMessageHandler(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("FrameOffsetMs must be >= 0")
                };
            });
            var service = new RemoteMediaStillCaptureService(CreateHttpClientWithHandler(handler));

            MediaStillCaptureResult result = await service.CaptureStillAsync(
                Guid.NewGuid(), -1, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
            Assert.Contains("FrameOffsetMs", result.ErrorMessage);
        }

        [Fact]
        public async Task CaptureStillAsync_ServerError_ReturnsExtractionFailedError()
        {
            var handler = new FakeHttpMessageHandler(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("ffmpeg not available")
                };
            });
            var service = new RemoteMediaStillCaptureService(CreateHttpClientWithHandler(handler));

            MediaStillCaptureResult result = await service.CaptureStillAsync(
                Guid.NewGuid(), 100, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.ExtractionFailed, result.Error);
        }
    }
}
