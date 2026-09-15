using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteEventRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static EventDto CreateEventDto(Guid? eventId = null, Guid? deviceId = null)
        {
            return new EventDto(
                Id: eventId ?? Guid.NewGuid(),
                DeviceId: deviceId ?? Guid.NewGuid(),
                ProviderEventId: "ring-event-123",
                EventType: "motion",
                OccurredAtUtc: DateTime.UtcNow.AddHours(-1),
                SnapshotUrl: "https://example.com/snapshot.jpg",
                MetadataJson: null,
                DiscoveredAtUtc: DateTime.UtcNow.AddHours(-1),
                DownloadedAtUtc: DateTime.UtcNow,
                ApiSourceHash: "hash-123",
                EventIntegrityHash: null
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
        public async Task GetAsync_CallsGetEventEndpoint_AndReturnsEvent()
        {
            var eventId = Guid.NewGuid();
            var eventDto = CreateEventDto(eventId: eventId);
            var json = JsonSerializer.Serialize(eventDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.GetAsync(eventId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/events/{eventId}", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.NotNull(result);
            Assert.Equal(eventId, result.Id);
        }

        [Fact]
        public async Task GetAsync_ReturnsNotFound_ReturnsNull()
        {
            var eventId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.GetAsync(eventId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var eventDto = CreateEventDto();
            var json = JsonSerializer.Serialize(eventDto, JsonOptions);
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

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.GetAsync(Guid.NewGuid(), cts.Token);

            Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task UpsertAsync_CallsPostEventEndpoint_AndReturnsEvent()
        {
            var eventToUpsert = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                ProviderEventId = "ring-456",
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow,
                SnapshotUrl = "https://example.com/snap.jpg",
                MetadataJson = null,
                DiscoveredAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = null,
                ApiSourceHash = "hash-456",
                EventIntegrityHash = null
            };

            var responseDto = CreateEventDto(eventId: eventToUpsert.Id, deviceId: eventToUpsert.DeviceId);
            var json = JsonSerializer.Serialize(responseDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                Assert.Equal(HttpMethod.Post, req.Method);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.UpsertAsync(eventToUpsert, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/events", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.NotNull(result);
            Assert.Equal(eventToUpsert.Id, result.Id);
        }

        [Fact]
        public async Task ListByDeviceAndDateRangeAsync_CallsCorrectEndpoint_AndReturnsEvents()
        {
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var event1 = CreateEventDto(deviceId: deviceId);
            var event2 = CreateEventDto(deviceId: deviceId);
            var json = JsonSerializer.Serialize(new List<EventDto> { event1, event2 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Contains($"/api/v1/events/by-device/{deviceId}", uri);
            Assert.Contains("fromUtc=", uri);
            Assert.Contains("toUtc=", uri);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ListByLocationAndDateRangeAsync_CallsCorrectEndpoint_AndReturnsEvents()
        {
            var locationId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var event1 = CreateEventDto();
            var event2 = CreateEventDto();
            var json = JsonSerializer.Serialize(new List<EventDto> { event1, event2 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListByLocationAndDateRangeAsync(locationId, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Contains($"/api/v1/events/by-location/{locationId}", uri);
            Assert.Contains("fromUtc=", uri);
            Assert.Contains("toUtc=", uri);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ListByDeviceEventTypeAndDateRangeAsync_CallsCorrectEndpoint_AndReturnsEvents()
        {
            var deviceId = Guid.NewGuid();
            var eventType = "motion";
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var event1 = CreateEventDto(deviceId: deviceId);
            var json = JsonSerializer.Serialize(new List<EventDto> { event1 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListByDeviceEventTypeAndDateRangeAsync(deviceId, eventType, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Contains($"/api/v1/events/by-type/{eventType}", uri);
            Assert.Contains($"deviceId={deviceId}", uri);
            Assert.Single(result);
        }

        [Fact]
        public async Task ListByLocationEventTypeAndDateRangeAsync_CallsCorrectEndpoint_AndReturnsEvents()
        {
            var locationId = Guid.NewGuid();
            var eventType = "person";
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var event1 = CreateEventDto();
            var json = JsonSerializer.Serialize(new List<EventDto> { event1 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListByLocationEventTypeAndDateRangeAsync(locationId, eventType, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Contains($"/api/v1/events/by-type/{eventType}", uri);
            Assert.Contains($"locationId={locationId}", uri);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetEventTypeSummaryAsync_CallsCorrectEndpoint_ReturnsSummary()
        {
            var locationId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var summary = new Dictionary<string, int> { { "motion", 5 }, { "person", 3 } };
            var json = JsonSerializer.Serialize(summary, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.GetEventTypeSummaryAsync(locationId, fromUtc, toUtc, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Contains($"/api/v1/events/summary/{locationId}", uri);
            Assert.Equal(5, result["motion"]);
            Assert.Equal(3, result["person"]);
        }

        [Fact]
        public async Task ListUnansweredOrFlaggedAsync_CallsCorrectEndpoint_ReturnsEvents()
        {
            var deviceId = Guid.NewGuid();
            var event1 = CreateEventDto(deviceId: deviceId);
            var json = JsonSerializer.Serialize(new List<EventDto> { event1 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListUnansweredOrFlaggedAsync(deviceId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Equal($"/api/v1/events/unanswered/{deviceId}", uri);
            Assert.Single(result);
        }

        [Fact]
        public async Task ListAsync_CallsCorrectEndpoint_ReturnsAllEvents()
        {
            var event1 = CreateEventDto();
            var event2 = CreateEventDto();
            var json = JsonSerializer.Serialize(new List<EventDto> { event1, event2 }, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.ListAsync(CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Equal("/api/v1/events/", uri);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task DeleteAsync_CallsDeleteEndpoint()
        {
            var eventId = Guid.NewGuid();

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            await repo.DeleteAsync(eventId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Delete, handler.CapturedRequest.Method);
            var uri = handler.CapturedRequest.RequestUri?.PathAndQuery;
            Assert.NotNull(uri);
            Assert.Equal($"/api/v1/events/{eventId}", uri);
        }

        [Fact]
        public async Task GetByProviderEventIdAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByProviderEventIdAsync(Guid.NewGuid(), "provider-event-123", CancellationToken.None));

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

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.GetByApiSourceHashAsync("hash-123", CancellationToken.None));

            Assert.Contains("Remote API doesn't yet support", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var eventEntity = new Event { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), ProviderEventId = "event-1", EventType = "motion" };

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.CreateAsync(eventEntity, CancellationToken.None));

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

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var eventEntity = new Event { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), ProviderEventId = "event-1", EventType = "motion" };

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.UpdateAsync(eventEntity, CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }

        [Fact]
        public async Task UpdateDownloadFailureAsync_Throws_NotSupportedException()
        {
            var handler = new FakeHttpMessageHandler(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => repo.UpdateDownloadFailureAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None));

            Assert.Contains("MAUI has no write path", ex.Message);
        }

        [Fact]
        public async Task GetAsync_DtoMappingCorrect_PropertiesMatchDomain()
        {
            var eventDto = new EventDto(
                Id: Guid.NewGuid(),
                DeviceId: Guid.NewGuid(),
                ProviderEventId: "ring-789",
                EventType: "person",
                OccurredAtUtc: new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                SnapshotUrl: "https://example.com/person.jpg",
                MetadataJson: "{\"confidence\":0.95}",
                DiscoveredAtUtc: new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
                DownloadedAtUtc: new DateTime(2024, 1, 15, 10, 40, 0, DateTimeKind.Utc),
                ApiSourceHash: "hash-789",
                EventIntegrityHash: "int-hash-789"
            );

            var json = JsonSerializer.Serialize(eventDto, JsonOptions);

            FakeHttpMessageHandler handler = new(async _ =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = CreateHttpClientWithHandler(handler);
            var repo = new RemoteEventRepository(httpClient);

            var result = await repo.GetAsync(eventDto.Id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(eventDto.Id, result.Id);
            Assert.Equal(eventDto.DeviceId, result.DeviceId);
            Assert.Equal(eventDto.ProviderEventId, result.ProviderEventId);
            Assert.Equal(eventDto.EventType, result.EventType);
            Assert.Equal(eventDto.OccurredAtUtc, result.OccurredAtUtc);
            Assert.Equal(eventDto.SnapshotUrl, result.SnapshotUrl);
            Assert.Equal(eventDto.MetadataJson, result.MetadataJson);
            Assert.Equal(eventDto.DiscoveredAtUtc, result.DiscoveredAtUtc);
            Assert.Equal(eventDto.DownloadedAtUtc, result.DownloadedAtUtc);
            Assert.Equal(eventDto.ApiSourceHash, result.ApiSourceHash);
            Assert.Equal(eventDto.EventIntegrityHash, result.EventIntegrityHash);
        }
    }
}
