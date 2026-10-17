using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Logging;
using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Drives <see cref="RingDeviceDiscoveryService.GetLocationsAsync(Guid, CancellationToken)"/> through a stub HTTP session
    /// (no network) across several syncs on a controlled clock. Config fetches run only when the account's config interval
    /// has elapsed since the last completed config sync; event feeds run on every sync. Each sync uses a fresh service
    /// instance so the in-memory locations cache (10-minute, wall-clock) does not short-circuit the second sync.
    /// </summary>
    public class RingConfigSyncGatingTests
    {
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly DateTime StartUtc = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        private const string ProfileUrl = "https://api.ring.com/clients_api/profile";
        private const string RingtonesUrl = "https://api.ring.com/clients_api/ringtones";
        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string GroupsUrl = $"https://api.ring.com/groups/v1/locations/{LocationId}/groups";
        private const string ModeUrl = $"https://prd-api-us.prd.rings.solutions/api/v1/mode/location/{LocationId}";
        private const string LocationEventsUrl = $"https://api.ring.com/clients_api/locations/{LocationId}/events";

        [Fact]
        public async Task RingConfigSyncGating_FirstSyncWithNoLastRun_RunsConfigFetchesAndMarksSync()
        {
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            var handler = CreateHandler();
            var writer = CreateWriter();

            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            Assert.Equal(1, handler.CountRequests(ProfileUrl));
            writer.Verify(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(StartUtc, state.LastConfigSyncUtc);
        }

        [Fact]
        public async Task RingConfigSyncGating_SecondSyncOneHourLater_SkipsConfigButRunsEventFeeds()
        {
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            var handler = CreateHandler();
            var writer = CreateWriter();
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            clock.Advance(TimeSpan.FromHours(1));
            var logger = new RecordingLogger();
            _ = await SyncAsync(handler, writer, state, clock, logger);

            Assert.Equal(1, handler.CountRequests(ProfileUrl));
            Assert.Equal(1, handler.CountRequests(GroupsUrl));
            writer.Verify(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(2, handler.CountRequests(LocationEventsUrl));
            Assert.Equal(StartUtc, state.LastConfigSyncUtc);
            (LogLevel Level, string Message) gateLine = Assert.Single(logger.Entries, e => e.Message.Contains("config sync not due", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Debug, gateLine.Level);
            Assert.DoesNotContain(AccountId.ToString(), gateLine.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(AccessToken, StringComparison.Ordinal));
        }

        [Fact]
        public async Task RingConfigSyncGating_SyncTwentyFiveHoursLater_RunsConfigAgain()
        {
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            var handler = CreateHandler();
            var writer = CreateWriter();
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            clock.Advance(TimeSpan.FromHours(25));
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            Assert.Equal(2, handler.CountRequests(ProfileUrl));
            writer.Verify(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Equal(StartUtc.AddHours(25), state.LastConfigSyncUtc);
        }

        [Fact]
        public async Task RingConfigSyncGating_IntervalChangedToSixHours_SyncSevenHoursLaterRunsConfig()
        {
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            var handler = CreateHandler();
            var writer = CreateWriter();
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            state.IntervalHours = 6;
            clock.Advance(TimeSpan.FromHours(7));
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            Assert.Equal(2, handler.CountRequests(ProfileUrl));
            Assert.Equal(StartUtc.AddHours(7), state.LastConfigSyncUtc);
        }

        [Fact]
        public async Task RingConfigSyncGating_AllAccountLevelFetchesFail_SyncNotMarkedAndNextSyncRetries()
        {
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            var handler = CreateHandler();
            handler.Routes[ProfileUrl] = (HttpStatusCode.InternalServerError, "{}");
            handler.Routes[RingtonesUrl] = (HttpStatusCode.InternalServerError, "{}");
            var writer = CreateWriter();

            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());
            Assert.Null(state.LastConfigSyncUtc);

            clock.Advance(TimeSpan.FromHours(1));
            _ = await SyncAsync(handler, writer, state, clock, new RecordingLogger());

            Assert.Equal(2, handler.CountRequests(ProfileUrl));
            Assert.Equal(2, handler.CountRequests(RingtonesUrl));
            Assert.Null(state.LastConfigSyncUtc);
        }

        /// <summary>Runs one sync on a fresh service instance (fresh locations cache) and returns the locations.</summary>
        private static async Task<IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>> SyncAsync(
            StubSessionHandler handler, Mock<IRingAccountSettingsWriter> writer, InMemorySyncState state, ManualTimeProvider clock, RecordingLogger logger)
        {
            var session = new Session("user@example.com", "password", handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            var service = new RingDeviceDiscoveryService(
                logger,
                sessionProvider.Object,
                settingsPersister: new RingAccountSettingsPersister(writer.Object, logger),
                eventHistoryWriter: new Mock<IRingEventHistoryWriter>().Object,
                syncState: state,
                timeProvider: clock);
            _ = await session.Authenticate();
            return await service.GetLocationsAsync(AccountId);
        }

        /// <summary>Serves account-settings, location-settings and the account's device list; event feeds are left to 404.</summary>
        private static StubSessionHandler CreateHandler()
        {
            var handler = new StubSessionHandler(AccessToken);
            handler.Routes[ProfileUrl] = (HttpStatusCode.OK, ReadFixture("profile-sanitized.json"));
            handler.Routes[RingtonesUrl] = (HttpStatusCode.OK, ReadFixture("ringtones-sanitized.json"));
            handler.Routes[LocationsUrl] = (HttpStatusCode.OK, $"{{\"user_locations\":[{{\"location_id\":\"{LocationId}\",\"name\":\"Home\",\"is_owner\":true}}]}}");
            handler.Routes[RingDevicesUrl] = (HttpStatusCode.OK, "{\"doorbots\":[],\"authorized_doorbots\":[],\"stickup_cams\":[],\"base_stations\":[],\"chimes\":[]}");
            handler.Routes[GroupsUrl] = (HttpStatusCode.OK, ReadFixture("light-groups-sanitized.json"));
            handler.Routes[ModeUrl] = (HttpStatusCode.OK, ReadFixture("location-mode-sanitized.json"));
            handler.Routes[$"{ModeUrl}/settings"] = (HttpStatusCode.OK, ReadFixture("location-mode-settings-sanitized.json"));
            handler.Routes[$"{ModeUrl}/sharing"] = (HttpStatusCode.OK, ReadFixture("location-mode-sharing-sanitized.json"));
            return handler;
        }

        private static Mock<IRingAccountSettingsWriter> CreateWriter()
        {
            var writer = new Mock<IRingAccountSettingsWriter>();
            _ = writer.Setup(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceRingtonesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingAccountRingtone>>(), It.IsAny<RingAccountRingtoneDefaults>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLightGroupSetAsync(It.IsAny<LightGroupSet>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationModeAsync(It.IsAny<RingLocationMode>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationModeSettingsAsync(It.IsAny<RingLocationModeSettings>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationModeSharingAsync(It.IsAny<RingLocationModeSharing>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return writer;
        }

        private static string ReadFixture(string name) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

        /// <summary>In-memory gate state; the interval and last-run time are settable so tests control the schedule.</summary>
        private sealed class InMemorySyncState : IRingAccountSyncState
        {
            public int IntervalHours { get; set; } = 24;

            public DateTime? LastConfigSyncUtc { get; private set; }

            public Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct) =>
                Task.FromResult(new RingConfigSyncState(IntervalHours, LastConfigSyncUtc));

            public Task MarkConfigSyncCompletedAsync(Guid providerAccountId, DateTime utcNow, CancellationToken ct)
            {
                LastConfigSyncUtc = utcNow;
                return Task.CompletedTask;
            }
        }

        /// <summary>A clock the test moves forward explicitly.</summary>
        private sealed class ManualTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;

            public ManualTimeProvider(DateTime startUtc) => _now = new DateTimeOffset(startUtc, TimeSpan.Zero);

            public void Advance(TimeSpan by) => _now = _now.Add(by);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        /// <summary>Answers OAuth and the configured URLs exactly; anything else is a 404. Never reaches the network.</summary>
        private sealed class StubSessionHandler : HttpMessageHandler
        {
            private readonly string _accessToken;
            private readonly List<string> _requestedUrls = [];

            public StubSessionHandler(string accessToken) => _accessToken = accessToken;

            public Dictionary<string, (HttpStatusCode Status, string Body)> Routes { get; } = [];

            public int CountRequests(string url) => _requestedUrls.Count(u => u == url);

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

        /// <summary>Keeps each log entry's level and text. Thread-safe: the sync's fire-and-forget metadata task logs on this instance.</summary>
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
