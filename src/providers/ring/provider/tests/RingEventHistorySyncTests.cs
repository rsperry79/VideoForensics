using Microsoft.Extensions.Logging;

using Moq;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Drives <see cref="RingDeviceDiscoveryService.GetLocationsAsync(Guid, CancellationToken)"/> through a real
    /// <see cref="Session"/> whose HTTP handler serves the sanitized captures (no network), into a mocked
    /// <see cref="IRingEventHistoryWriter"/>. Checks each event feed reaches the writer with the mapped data, that a
    /// failing feed logs an error naming only the feed, and that a 404 is logged at information level and skipped.
    /// </summary>
    public class RingEventHistorySyncTests
    {
        private const string LocationId = "11111111-2222-3333-4444-555555555555";
        private const string DoorbotId = "86401464";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private static readonly Guid AccountId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string DoorbotHistoryUrl = "https://api.ring.com/clients_api/doorbots/86401464/history";
        private const string VideoSearchUrl = "https://api.ring.com/clients_api/video_search/history";
        private const string LocationEventsUrl = "https://api.ring.com/clients_api/locations/11111111-2222-3333-4444-555555555555/events";
        private const string LocationHistoryUrl = "https://api.ring.com/evm/v2/history/locations/11111111-2222-3333-4444-555555555555";
        private const string AppLocationHistoryUrl = "https://prd-api-us.prd.rings.solutions/api/v1/rs/history";

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_CapturedDoorbotHistory_WritesEveryEventOnce()
        {
            StubSessionHandler handler = CreateHandler();
            Mock<IRingEventHistoryWriter> writer = CreateWriter();

            await SyncAsync(handler, writer, new RecordingLogger());

            List<DoorbotHistoryEvent> events = JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(ReadFixture("doorbot-history-sanitized.json"))!;
            string firstId = events[0].Id!.Value.ToString(CultureInfo.InvariantCulture);
            writer.Verify(w => w.ReplaceDoorbotHistoryEventAsync(
                It.Is<RingDoorbotHistoryEvent>(e => e.ProviderEventId == firstId),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            writer.Verify(w => w.ReplaceDoorbotHistoryEventAsync(
                It.IsAny<RingDoorbotHistoryEvent>(),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.Exactly(events.Count));
            Assert.Equal(1, handler.CountRequests(DoorbotHistoryUrl));
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_CapturedVideoSearch_WritesEveryItemForTheAccount()
        {
            StubSessionHandler handler = CreateHandler();
            Mock<IRingEventHistoryWriter> writer = CreateWriter();

            await SyncAsync(handler, writer, new RecordingLogger());

            VideoSearchResponse response = JsonSerializer.Deserialize<VideoSearchResponse>(ReadFixture("video-search-sanitized.json"))!;
            string firstDingId = response.VideoSearch[0].DingId;
            writer.Verify(w => w.ReplaceVideoSearchEventAsync(
                AccountId,
                It.Is<RingVideoSearchEvent>(e => e.ProviderEventId == firstDingId),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            writer.Verify(w => w.ReplaceVideoSearchEventAsync(
                AccountId,
                It.IsAny<RingVideoSearchEvent>(),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.Exactly(response.VideoSearch.Count));
            Assert.Equal(1, handler.CountRequests(VideoSearchUrl));
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_CapturedLocationEvents_WritesPageCursorAndEveryEvent()
        {
            StubSessionHandler handler = CreateHandler();
            Mock<IRingEventHistoryWriter> writer = CreateWriter();

            await SyncAsync(handler, writer, new RecordingLogger());

            LocationEventsResponse response = JsonSerializer.Deserialize<LocationEventsResponse>(ReadFixture("location-events-sanitized.json"))!;
            writer.Verify(w => w.ReplaceLocationEventPageAsync(
                It.Is<RingLocationEventPage>(p => p.ProviderLocationId == LocationId && p.PaginationKey == response.Meta.PaginationKey),
                It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationEventAsync(
                It.Is<RingLocationEvent>(e => e.ProviderEventId == response.Events[0].EventId),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            writer.Verify(w => w.ReplaceLocationEventAsync(
                It.IsAny<RingLocationEvent>(),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.Exactly(response.Events.Count));
            Assert.Equal(1, handler.CountRequests(LocationEventsUrl));
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_CapturedLocationHistory_WritesPageCursorAndEveryItem()
        {
            StubSessionHandler handler = CreateHandler();
            Mock<IRingEventHistoryWriter> writer = CreateWriter();

            await SyncAsync(handler, writer, new RecordingLogger());

            LocationHistoryResponse response = JsonSerializer.Deserialize<LocationHistoryResponse>(ReadFixture("location-history-sanitized.json"))!;
            writer.Verify(w => w.ReplaceLocationHistoryPageAsync(
                It.Is<RingLocationHistoryPage>(p => p.ProviderLocationId == LocationId && p.PaginationKey == response.PaginationKey),
                It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationHistoryEventAsync(
                It.IsAny<RingLocationHistoryEvent>(),
                It.IsAny<RingEventDetails>(),
                It.IsAny<CancellationToken>()), Times.Exactly(response.Items.Count));
            Assert.Equal(1, handler.CountRequests(LocationHistoryUrl));
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_CapturedAppLocationHistory_WritesEveryMessage()
        {
            StubSessionHandler handler = CreateHandler();
            Mock<IRingEventHistoryWriter> writer = CreateWriter();

            await SyncAsync(handler, writer, new RecordingLogger());

            List<AppLocationHistoryMessage> messages = JsonSerializer.Deserialize<List<AppLocationHistoryMessage>>(ReadFixture("app-location-history-sanitized.json"))!;
            string firstEventId = messages[0].Context!.EventId!;
            writer.Verify(w => w.ReplaceAppLocationHistoryMessageAsync(
                It.Is<RingAppLocationHistoryMessage>(m => m.EventId == firstEventId),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            writer.Verify(w => w.ReplaceAppLocationHistoryMessageAsync(
                It.IsAny<RingAppLocationHistoryMessage>(),
                It.IsAny<CancellationToken>()), Times.Exactly(messages.Count));
            Assert.Equal(1, handler.CountRequests(AppLocationHistoryUrl));
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_DoorbotHistoryFails_LogsErrorNamingFeedAndOtherFeedsStillWrite()
        {
            StubSessionHandler handler = CreateHandler();
            handler.Routes[DoorbotHistoryUrl] = (HttpStatusCode.InternalServerError, "{}");
            Mock<IRingEventHistoryWriter> writer = CreateWriter();
            var logger = new RecordingLogger();

            await SyncAsync(handler, writer, logger);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("doorbot history", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("api.ring.com", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("prd-api-us", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains(AccessToken, StringComparison.Ordinal));
            writer.Verify(w => w.ReplaceDoorbotHistoryEventAsync(
                It.IsAny<RingDoorbotHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>()), Times.Never);
            writer.Verify(w => w.ReplaceVideoSearchEventAsync(
                It.IsAny<Guid>(), It.IsAny<RingVideoSearchEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            writer.Verify(w => w.ReplaceLocationEventPageAsync(It.IsAny<RingLocationEventPage>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationHistoryPageAsync(It.IsAny<RingLocationHistoryPage>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceAppLocationHistoryMessageAsync(It.IsAny<RingAppLocationHistoryMessage>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RingEventHistorySync_GetLocationsAsync_LocationHistoryNotFound_LogsInformationAndSkipsWithoutWarning()
        {
            StubSessionHandler handler = CreateHandler();
            _ = handler.Routes.Remove(LocationHistoryUrl);
            Mock<IRingEventHistoryWriter> writer = CreateWriter();
            var logger = new RecordingLogger();

            await SyncAsync(handler, writer, logger);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("location history", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
            writer.Verify(w => w.ReplaceLocationHistoryPageAsync(It.IsAny<RingLocationHistoryPage>(), It.IsAny<CancellationToken>()), Times.Never);
            writer.Verify(w => w.ReplaceLocationHistoryEventAsync(It.IsAny<RingLocationHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>()), Times.Never);
            writer.Verify(w => w.ReplaceLocationEventPageAsync(It.IsAny<RingLocationEventPage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Runs one sync for the account through the stub session.</summary>
        private static async Task SyncAsync(StubSessionHandler handler, Mock<IRingEventHistoryWriter> writer, RecordingLogger logger)
        {
            RingDeviceDiscoveryService service = CreateService(handler, writer, logger, out Session session);
            _ = await session.Authenticate();
            _ = await service.GetLocationsAsync(AccountId);
        }

        private static RingDeviceDiscoveryService CreateService(
            StubSessionHandler handler, Mock<IRingEventHistoryWriter> writer, RecordingLogger logger, out Session session)
        {
            session = new Session("user@example.com", "password", handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            return new RingDeviceDiscoveryService(
                logger,
                sessionProvider.Object,
                eventHistoryWriter: writer.Object);
        }

        /// <summary>Serves the sanitized captures for every event-history URL, with one doorbot at one location.</summary>
        private static StubSessionHandler CreateHandler()
        {
            var handler = new StubSessionHandler(AccessToken);
            handler.Routes[LocationsUrl] = (HttpStatusCode.OK, $"{{\"user_locations\":[{{\"location_id\":\"{LocationId}\",\"name\":\"Home\",\"is_owner\":true}}]}}");
            handler.Routes[RingDevicesUrl] = (HttpStatusCode.OK,
                $"{{\"doorbots\":[{{\"id\":{DoorbotId},\"description\":\"Front Door\",\"location_id\":\"{LocationId}\",\"kind\":\"doorbell_graham_cracker\"}}],\"authorized_doorbots\":[],\"stickup_cams\":[],\"base_stations\":[],\"chimes\":[]}}");
            handler.Routes[DoorbotHistoryUrl] = (HttpStatusCode.OK, ReadFixture("doorbot-history-sanitized.json"));
            handler.Routes[VideoSearchUrl] = (HttpStatusCode.OK, ReadFixture("video-search-sanitized.json"));
            handler.Routes[LocationEventsUrl] = (HttpStatusCode.OK, ReadFixture("location-events-sanitized.json"));
            handler.Routes[LocationHistoryUrl] = (HttpStatusCode.OK, ReadFixture("location-history-sanitized.json"));
            handler.Routes[AppLocationHistoryUrl] = (HttpStatusCode.OK, ReadFixture("app-location-history-sanitized.json"));
            return handler;
        }

        private static Mock<IRingEventHistoryWriter> CreateWriter()
        {
            var writer = new Mock<IRingEventHistoryWriter>();
            _ = writer.Setup(w => w.ReplaceVideoSearchEventAsync(It.IsAny<Guid>(), It.IsAny<RingVideoSearchEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationEventPageAsync(It.IsAny<RingLocationEventPage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationEventAsync(It.IsAny<RingLocationEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationHistoryPageAsync(It.IsAny<RingLocationHistoryPage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationHistoryEventAsync(It.IsAny<RingLocationHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceDoorbotHistoryEventAsync(It.IsAny<RingDoorbotHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceAppLocationHistoryMessageAsync(It.IsAny<RingAppLocationHistoryMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return writer;
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        }

        /// <summary>Answers OAuth and the configured URLs exactly; anything else is a 404. Never reaches the network.</summary>
        private sealed class StubSessionHandler : HttpMessageHandler
        {
            private readonly string _accessToken;
            private readonly List<string> _requestedUrls = [];

            public StubSessionHandler(string accessToken)
            {
                _accessToken = accessToken;
            }

            public Dictionary<string, (HttpStatusCode Status, string Body)> Routes { get; } = [];

            public int CountRequests(string url)
            {
                return _requestedUrls.Count(u => u == url);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;
                _requestedUrls.Add(url);

                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    string token = $"{{\"access_token\":\"{_accessToken}\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(token), RequestMessage = request });
                }

                (HttpStatusCode status, string body) = Routes.TryGetValue(url, out (HttpStatusCode Status, string Body) route)
                    ? (route.Status, route.Body)
                    : (HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");

                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body), RequestMessage = request });
            }
        }

        /// <summary>Keeps each log entry's level and formatted text so a test can assert an error, information entry, or no token text.</summary>
        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

            public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
