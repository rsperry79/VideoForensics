using System.Net;
using System.Text;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteReleaseChannelServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? CapturedRequest { get; private set; }
            public string? CapturedBody { get; private set; }
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _factory;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> factory)
            {
                _factory = factory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                CapturedRequest = request;
                CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
                return await _factory(request);
            }
        }

        private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        /// <summary>
        /// Mimics a real transport: honours the token passed to SendAsync, so a cancelled token
        /// only surfaces as an exception if the service forwards it.
        /// </summary>
        private sealed class CancellationRespectingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(JsonResponse("{\"channel\":\"Stable\"}"));
            }
        }

        [Fact]
        public async Task GetReleaseChannelAsync_ServerReturnsTesting_ReturnsTestingEnum()
        {
            // Arrange
            string json = JsonSerializer.Serialize(new ReleaseChannelDto("Testing"), JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return JsonResponse(json);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act
            UpdateReleaseChannel channel = await service.GetReleaseChannelAsync(CancellationToken.None);

            // Assert
            Assert.Equal(UpdateReleaseChannel.Testing, channel);
        }

        [Fact]
        public async Task GetReleaseChannelAsync_Called_HitsVersionedReleaseChannelRoute()
        {
            // Arrange
            string json = JsonSerializer.Serialize(new ReleaseChannelDto("Stable"), JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return JsonResponse(json);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act
            _ = await service.GetReleaseChannelAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest!.Method);
            Assert.Equal("/api/v1/update-settings/release-channel", handler.CapturedRequest.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task SetReleaseChannelAsync_Stable_SendsCamelCaseChannelBodyAndReturnsParsedEnum()
        {
            // Arrange
            string json = JsonSerializer.Serialize(new ReleaseChannelDto("Stable"), JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return JsonResponse(json);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act
            UpdateReleaseChannel result = await service.SetReleaseChannelAsync(UpdateReleaseChannel.Stable, CancellationToken.None);

            // Assert
            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Put, handler.CapturedRequest!.Method);
            Assert.Equal("/api/v1/update-settings/release-channel", handler.CapturedRequest.RequestUri!.AbsolutePath);
            Assert.Equal("{\"channel\":\"Stable\"}", handler.CapturedBody);
            Assert.Equal(UpdateReleaseChannel.Stable, result);
        }

        [Fact]
        public async Task GetReleaseChannelAsync_NonSuccessStatus_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetReleaseChannelAsync(CancellationToken.None));
        }

        [Fact]
        public async Task SetReleaseChannelAsync_BadRequestStatus_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(() => service.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, CancellationToken.None));
        }

        [Fact]
        public async Task GetReleaseChannelAsync_UnknownChannelString_ThrowsInvalidOperationException()
        {
            // Arrange
            string json = JsonSerializer.Serialize(new ReleaseChannelDto("Nightly"), JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return JsonResponse(json);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReleaseChannelAsync(CancellationToken.None));
        }

        [Fact]
        public async Task SetReleaseChannelAsync_UnknownChannelInResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            string json = JsonSerializer.Serialize(new ReleaseChannelDto("Bogus"), JsonOptions);
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return JsonResponse(json);
            });
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetReleaseChannelAsync(UpdateReleaseChannel.Stable, CancellationToken.None));
        }

        [Fact]
        public async Task GetReleaseChannelAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var handler = new CancellationRespectingHandler();
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetReleaseChannelAsync(cts.Token));
        }

        [Fact]
        public async Task SetReleaseChannelAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var handler = new CancellationRespectingHandler();
            var service = new RemoteReleaseChannelService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            // Act & Assert
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SetReleaseChannelAsync(UpdateReleaseChannel.Stable, cts.Token));
        }
    }
}
