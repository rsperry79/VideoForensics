using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    /// <summary>Round-trip tests for the remote event page and event-detail reads, using a fake HTTP handler.</summary>
    public class RemoteEventPageAndDetailTests
    {
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

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
        }

        private static Dictionary<string, List<string>> ParseQuery(Uri uri)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                string key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
                string value = eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..]);
                if (!result.TryGetValue(key, out List<string>? values))
                {
                    values = new List<string>();
                    result[key] = values;
                }

                values.Add(value);
            }

            return result;
        }

        private static EventDto CreateEventDto(Guid id)
        {
            return new EventDto(
                Id: id,
                DeviceId: Guid.NewGuid(),
                ProviderEventId: "prov-1",
                EventType: "motion",
                OccurredAtUtc: new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                SnapshotUrl: null,
                MetadataJson: null,
                DiscoveredAtUtc: new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                DownloadedAtUtc: null,
                ApiSourceHash: null,
                EventIntegrityHash: null);
        }

        [Fact]
        public async Task RemoteEventRepository_ListEventPage_SendsDevicesRangeExcludeAndPaging_InQueryString()
        {
            Guid deviceA = Guid.NewGuid();
            Guid deviceB = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse("{}")));
            var repo = new RemoteEventRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await repo.ListPageByDevicesAndDateRangeAsync(
                new[] { deviceA, deviceB }, fromUtc, toUtc, new[] { "person", "ring" }, null, skip: 5, take: 10, CancellationToken.None);

            Uri uri = handler.CapturedRequest!.RequestUri!;
            Assert.Equal("/api/v1/events/page", uri.AbsolutePath);
            Dictionary<string, List<string>> query = ParseQuery(uri);
            Assert.Equal(new[] { deviceA.ToString("D"), deviceB.ToString("D") }, query["deviceIds"]);
            Assert.Equal(new[] { fromUtc.ToString("o") }, query["from"]);
            Assert.Equal(new[] { toUtc.ToString("o") }, query["to"]);
            Assert.Equal(new[] { "person", "ring" }, query["excludeTypes"]);
            Assert.Equal(new[] { "5" }, query["skip"]);
            Assert.Equal(new[] { "10" }, query["take"]);
        }

        [Fact]
        public async Task RemoteEventRepository_ListEventPage_IncludeTypes_SendsEscapedIncludeTypesQueryParams()
        {
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse("{}")));
            var repo = new RemoteEventRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await repo.ListPageByDevicesAndDateRangeAsync(
                new[] { Guid.NewGuid() },
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                null, new[] { "ring doorbell", "person" }, skip: 0, take: 10, CancellationToken.None);

            Dictionary<string, List<string>> query = ParseQuery(handler.CapturedRequest!.RequestUri!);
            Assert.Equal(new[] { "ring doorbell", "person" }, query["includeTypes"]);
            Assert.False(query.ContainsKey("excludeTypes"));
        }

        [Fact]
        public async Task RemoteEventRepository_ListEventPage_ResponseItemsAndTotal_MapToEventPage()
        {
            Guid idOne = Guid.NewGuid();
            Guid idTwo = Guid.NewGuid();
            string json = JsonSerializer.Serialize(new
            {
                items = new[] { CreateEventDto(idOne), CreateEventDto(idTwo) },
                totalCount = 42,
            });
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(json)));
            var repo = new RemoteEventRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            EventPage page = await repo.ListPageByDevicesAndDateRangeAsync(
                new[] { Guid.NewGuid() },
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                null, null, skip: 0, take: 2, CancellationToken.None);

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(idOne, page.Items[0].Id);
            Assert.Equal(idTwo, page.Items[1].Id);
            Assert.Equal(42, page.TotalCount);
        }

        [Fact]
        public async Task RemoteEventDetailRepository_GetDetails_SendsRepeatedIds_AndMapsResponse()
        {
            Guid eventId = Guid.NewGuid();
            Guid otherId = Guid.NewGuid();
            var detailDto = new EventDetailDto(
                EventId: eventId,
                Detection: new EventDetailDto.EventDetectionDto(true, false, "Motion", "full", "short", 0.5m, 0.1m, 0.9m, "v1"),
                Occurrences: new[] { new EventDetailDto.EventOccurrenceDto("Motion", new DateTime(2024, 1, 15, 12, 0, 1, DateTimeKind.Utc)) },
                Zones: new[] { new EventDetailDto.EventZoneDto("zone-1", "Driveway", 0.8m) },
                People: new[] { new EventDetailDto.EventPersonDto("profile-1", "Alex", 0.7m, "https://example.test/t.jpg") },
                SecurityAlerts: new[] { new EventDetailDto.EventSecurityAlertDto("High", "alert") });
            string json = JsonSerializer.Serialize(new[] { detailDto });
            var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(json)));
            var repo = new RemoteEventDetailRepository(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            IReadOnlyList<EventDetail> details = await repo.GetDetailsAsync(new[] { eventId, otherId }, CancellationToken.None);

            Uri uri = handler.CapturedRequest!.RequestUri!;
            Assert.Equal("/api/v1/events/details", uri.AbsolutePath);
            Assert.Equal(new[] { eventId.ToString("D"), otherId.ToString("D") }, ParseQuery(uri)["eventIds"]);

            EventDetail detail = Assert.Single(details);
            Assert.Equal(eventId, detail.EventId);
            Assert.Equal("Motion", detail.Detection!.DetectionType);
            Assert.Equal("zone-1", Assert.Single(detail.Zones).ZoneId);
            Assert.Equal("profile-1", Assert.Single(detail.People).ProfileId);
            Assert.Equal("alert", Assert.Single(detail.SecurityAlerts).AlertText);
            Assert.Equal("Motion", Assert.Single(detail.Occurrences).DetectionType);
        }
    }
}
