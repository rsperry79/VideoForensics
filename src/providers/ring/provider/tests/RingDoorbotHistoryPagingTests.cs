using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Drives <see cref="RingDeviceDiscoveryService.GetLocationsAsync(Guid, CancellationToken)"/> through a real
    /// <see cref="Session"/> whose HTTP handler answers the doorbot history URL per query string (no network), so the
    /// paging loop's <c>limit</c> and <c>older_than</c> parameters can be checked page by page.
    /// </summary>
    public class RingDoorbotHistoryPagingTests
    {
        private const string LocationId = "11111111-2222-3333-4444-555555555555";
        private const string DoorbotId = "86401464";
        private const string DoorbotName = "Front Door";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private const int PageSize = 100;
        private const int PageCap = 200;
        private static readonly Guid AccountId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string DoorbotHistoryPath = "/clients_api/doorbots/86401464/history";

        [Fact]
        public async Task RingDoorbotHistoryPaging_ThreePages_RequestsOlderThanFromPreviousOldestAndWritesEachOnce()
        {
            // 100, 100, 37: the third page is short, so the loop must stop after it.
            var handler = CreateHandler(olderThan =>
            {
                long start = olderThan ?? 1000;
                return start switch
                {
                    1000 => Page(1000, 100),
                    901 => Page(900, 100),
                    801 => Page(800, 37),
                    _ => "[]",
                };
            });
            var writer = CreateWriter(out List<string> written);

            await SyncAsync(handler, writer, new RecordingLogger());

            Assert.Equal(
                [
                    $"{DoorbotHistoryPath}?limit=100",
                    $"{DoorbotHistoryPath}?limit=100&older_than=901",
                    $"{DoorbotHistoryPath}?limit=100&older_than=801",
                ],
                handler.HistoryQueries);
            Assert.Equal(237, written.Count);
            Assert.Equal(237, written.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public async Task RingDoorbotHistoryPaging_EmptyFirstPage_MakesOneRequestAndWritesNothing()
        {
            var handler = CreateHandler(_ => "[]");
            var writer = CreateWriter(out List<string> written);

            await SyncAsync(handler, writer, new RecordingLogger());

            Assert.Equal([$"{DoorbotHistoryPath}?limit=100"], handler.HistoryQueries);
            Assert.Empty(written);
        }

        [Fact]
        public async Task RingDoorbotHistoryPaging_RepeatedOldestId_StopsWithoutRequestingTheSameCursorAgain()
        {
            // Every request returns the same full page, so the oldest id never moves past the cursor.
            var handler = CreateHandler(_ => Page(1000, 100));
            var writer = CreateWriter(out List<string> written);

            await SyncAsync(handler, writer, new RecordingLogger());

            Assert.Equal(
                [
                    $"{DoorbotHistoryPath}?limit=100",
                    $"{DoorbotHistoryPath}?limit=100&older_than=901",
                ],
                handler.HistoryQueries);
            Assert.Equal(100, written.Count);
        }

        [Fact]
        public async Task RingDoorbotHistoryPaging_PageCapReached_StopsAtTwoHundredPagesAndLogsDoorbellNameAtInformation()
        {
            // Every page is full and older than the last, so only the cap can end the loop.
            // older_than is exclusive, so the page after cursor X starts at X - 1.
            var handler = CreateHandler(olderThan => Page(olderThan.HasValue ? olderThan.Value - 1 : 1_000_000, PageSize));
            var writer = CreateWriter(out List<string> written);
            var logger = new RecordingLogger();

            await SyncAsync(handler, writer, logger);

            Assert.Equal(PageCap, handler.HistoryQueries.Count);
            Assert.Equal(PageCap * PageSize, written.Count);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information
                && e.Message.Contains(DoorbotName, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("api.ring.com", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains(AccessToken, StringComparison.Ordinal));
        }

        /// <summary>Runs one sync for the account through the stub session.</summary>
        private static async Task SyncAsync(PagingStubHandler handler, Mock<IRingEventHistoryWriter> writer, RecordingLogger logger)
        {
            var session = new Session("user@example.com", "password", handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object, eventHistoryWriter: writer.Object);

            await session.Authenticate();
            _ = await service.GetLocationsAsync(AccountId);
        }

        /// <summary>Serves a doorbot with one location; the history URL is answered by <paramref name="historyBody"/> from the parsed older_than.</summary>
        private static PagingStubHandler CreateHandler(Func<long?, string> historyBody)
        {
            var handler = new PagingStubHandler(AccessToken, historyBody);
            handler.Routes[LocationsUrl] = (HttpStatusCode.OK, $"{{\"user_locations\":[{{\"location_id\":\"{LocationId}\",\"name\":\"Home\",\"is_owner\":true}}]}}");
            handler.Routes[RingDevicesUrl] = (HttpStatusCode.OK,
                $"{{\"doorbots\":[{{\"id\":{DoorbotId},\"description\":\"{DoorbotName}\",\"location_id\":\"{LocationId}\",\"kind\":\"doorbell_graham_cracker\"}}],\"authorized_doorbots\":[],\"stickup_cams\":[],\"base_stations\":[],\"chimes\":[]}}");
            return handler;
        }

        /// <summary>A writer that records each persisted doorbot history event id. Other feeds are accepted and ignored.</summary>
        private static Mock<IRingEventHistoryWriter> CreateWriter(out List<string> written)
        {
            var ids = new List<string>();
            written = ids;
            var writer = new Mock<IRingEventHistoryWriter>();
            _ = writer.Setup(w => w.ReplaceDoorbotHistoryEventAsync(It.IsAny<RingDoorbotHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>()))
                .Callback<RingDoorbotHistoryEvent, RingEventDetails, CancellationToken>((e, _, _) => ids.Add(e.ProviderEventId!))
                .Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceVideoSearchEventAsync(It.IsAny<Guid>(), It.IsAny<RingVideoSearchEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationEventPageAsync(It.IsAny<RingLocationEventPage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationEventAsync(It.IsAny<RingLocationEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationHistoryPageAsync(It.IsAny<RingLocationHistoryPage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationHistoryEventAsync(It.IsAny<RingLocationHistoryEvent>(), It.IsAny<RingEventDetails>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceAppLocationHistoryMessageAsync(It.IsAny<RingAppLocationHistoryMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return writer;
        }

        /// <summary>
        /// A page of <paramref name="count"/> history events with descending ids starting at <paramref name="newestId"/>.
        /// Ids are unique per page sequence, so a repeated id in the writer output means an event was persisted twice.
        /// </summary>
        private static string Page(long newestId, int count)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                long id = newestId - i;
                sb.Append(CultureInfo.InvariantCulture, $"{{\"id\":{id},\"created_at\":\"2026-01-01T00:00:00Z\",\"answered\":false,\"kind\":\"ding\",\"events\":[]}}");
            }

            return sb.Append(']').ToString();
        }

        /// <summary>Answers OAuth, the locations and device lists, and the doorbot history per query; anything else is a 404. Never reaches the network.</summary>
        private sealed class PagingStubHandler : HttpMessageHandler
        {
            private readonly string _accessToken;
            private readonly Func<long?, string> _historyBody;
            private readonly ConcurrentQueue<string> _historyQueries = new();

            public PagingStubHandler(string accessToken, Func<long?, string> historyBody)
            {
                _accessToken = accessToken;
                _historyBody = historyBody;
            }

            public Dictionary<string, (HttpStatusCode Status, string Body)> Routes { get; } = [];

            /// <summary>Path plus query of every doorbot history request, in order.</summary>
            public List<string> HistoryQueries => [.. _historyQueries];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;

                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    string token = $"{{\"access_token\":\"{_accessToken}\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(token), RequestMessage = request });
                }

                if (request.RequestUri!.AbsolutePath == DoorbotHistoryPath)
                {
                    string query = request.RequestUri.Query;
                    _historyQueries.Enqueue(request.RequestUri.AbsolutePath + query);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_historyBody(ParseOlderThan(query))),
                        RequestMessage = request,
                    });
                }

                (HttpStatusCode status, string body) = Routes.TryGetValue(url, out (HttpStatusCode Status, string Body) route)
                    ? (route.Status, route.Body)
                    : (HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");

                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body), RequestMessage = request });
            }

            /// <summary>Reads older_than from a "?limit=N&amp;older_than=X" query string, or null when absent.</summary>
            private static long? ParseOlderThan(string query)
            {
                foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = pair.Split('=', 2);
                    if (parts[0] == "older_than" && parts.Length == 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                    {
                        return value;
                    }
                }

                return null;
            }
        }

        /// <summary>Keeps each log entry's level and formatted text so a test can assert a warning, information entry, or no token text.</summary>
        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

            public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                _entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
