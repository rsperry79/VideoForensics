using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteLiveViewSessionServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly Uri BaseUri = new("https://example.test");

        [Fact]
        public async Task StartAsync_ValidResponse_ReturnsDomainSession()
        {
            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var startedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

            var dto = new LiveViewSessionDto(
                Id: sessionId,
                DeviceId: deviceId,
                TriggerReason: "Manual",
                State: "Active",
                StartedAtUtc: startedAt,
                EndedAtUtc: null,
                LastExtendedAtUtc: startedAt,
                IsSustained: false,
                SustainedSinceUtc: null,
                PromotionReason: null,
                OperatorId: null,
                StopReason: null,
                ProviderSessionRef: "provider-ref-1");

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
                return response;
            });

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            LiveViewSession result = await service.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);

            Assert.Equal(sessionId, result.Id);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Equal(LiveViewTriggerReason.Manual, result.TriggerReason);
            Assert.Equal(LiveViewSessionState.Active, result.State);
            Assert.Equal(startedAt, result.StartedAtUtc);
            Assert.Equal("provider-ref-1", result.ProviderSessionRef);
            Assert.Null(result.OperatorId);
        }

        [Fact]
        public async Task StartAsync_ServerError_Throws()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
                new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            _ = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                _ = await service.StartAsync(Guid.NewGuid(), LiveViewTriggerReason.Manual, null, CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetActiveSessionAsync_NotFound_ReturnsNull()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            LiveViewSession? result = await service.GetActiveSessionAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task StopAsync_ForwardsCancellationToken()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.StopAsync(Guid.NewGuid(), "ManualStop", cts.Token);
            });
        }

        [Fact]
        public async Task ExtendAsync_UnknownSession_ThrowsKeyNotFound()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            _ = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await service.ExtendAsync(Guid.NewGuid(), CancellationToken.None);
            });
        }

        [Fact]
        public async Task PromoteToSustainedAsync_UnknownSession_ThrowsKeyNotFound()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            _ = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                _ = await service.PromoteToSustainedAsync(Guid.NewGuid(), "Sustained bitrate degradation", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GetConnectionForSessionAsync_Always_ThrowsNotSupported()
        {
            int requestCount = 0;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                requestCount++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                _ = await service.GetConnectionForSessionAsync(Guid.NewGuid(), CancellationToken.None);
            });

            Assert.Equal(0, requestCount);
        }

        [Fact]
        public async Task AllCalls_UseV1Routes()
        {
            var capturedPaths = new List<string>();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedPaths.Add(request.RequestUri!.AbsolutePath);
                var sessionDto = new LiveViewSessionDto(
                    Id: Guid.NewGuid(),
                    DeviceId: Guid.NewGuid(),
                    TriggerReason: "Manual",
                    State: "Active",
                    StartedAtUtc: DateTime.UtcNow,
                    EndedAtUtc: null,
                    LastExtendedAtUtc: DateTime.UtcNow,
                    IsSustained: false,
                    SustainedSinceUtc: null,
                    PromotionReason: null,
                    OperatorId: null,
                    StopReason: null,
                    ProviderSessionRef: null);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(sessionDto), System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var service = new RemoteLiveViewSessionService(new HttpClient(handler) { BaseAddress = BaseUri });
            Guid sessionId = Guid.NewGuid();
            Guid deviceId = Guid.NewGuid();

            _ = await service.StartAsync(deviceId, LiveViewTriggerReason.Manual, null, CancellationToken.None);
            await service.ExtendAsync(sessionId, CancellationToken.None);
            _ = await service.PromoteToSustainedAsync(sessionId, "reason", CancellationToken.None);
            _ = await service.DemoteFromSustainedAsync(sessionId, CancellationToken.None);
            await service.StopAsync(sessionId, "ManualStop", CancellationToken.None);
            _ = await service.GetActiveSessionAsync(deviceId, CancellationToken.None);

            Assert.Equal(6, capturedPaths.Count);
            foreach (string path in capturedPaths)
            {
                Assert.StartsWith("/api/v1/live-view/", path);
            }
        }

        private class CaptureHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await _handler(request, cancellationToken);
            }
        }
    }
}
