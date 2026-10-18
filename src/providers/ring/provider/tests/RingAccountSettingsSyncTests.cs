using System.Collections.Concurrent;
using System.Net;
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
    /// <see cref="Session"/> whose HTTP handler serves the sanitized captures (no network), into a
    /// <see cref="RingAccountSettingsPersister"/> over a mocked writer. Checks each fetch reaches its writer
    /// with the mapped data, and that one failing fetch does not stop the rest of the sync.
    /// </summary>
    public class RingAccountSettingsSyncTests
    {
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private const string ProfileUrl = "https://api.ring.com/clients_api/profile";
        private const string RingtonesUrl = "https://api.ring.com/clients_api/ringtones";
        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string GroupsUrl = $"https://api.ring.com/groups/v1/locations/{LocationId}/groups";
        private const string ModeUrl = $"https://prd-api-us.prd.rings.solutions/api/v1/mode/location/{LocationId}";

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_CapturedProfile_WritesProfileOnceWithoutToken()
        {
            var handler = CreateHandler();
            var writer = CreateWriter();
            var logger = new RecordingLogger();

            _ = await SyncAsync(handler, writer, logger);

            Profile profile = JsonSerializer.Deserialize<ProfileResponse>(ReadFixture("profile-sanitized.json"))!.Profile;
            writer.Verify(w => w.ReplaceProfileAsync(It.Is<RingAccountProfile>(p => p.ProviderUserId == profile.Id && p.RingAccountId == AccountId), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, handler.CountRequests(ProfileUrl));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(AccessToken, StringComparison.Ordinal));
        }

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_CapturedRingtones_WritesRingtonesAndDefaultsOnce()
        {
            var handler = CreateHandler();
            var writer = CreateWriter();

            _ = await SyncAsync(handler, writer, new RecordingLogger());

            RingtonesResponse response = JsonSerializer.Deserialize<RingtonesResponse>(ReadFixture("ringtones-sanitized.json"))!;
            writer.Verify(w => w.ReplaceRingtonesAsync(
                AccountId,
                It.Is<IReadOnlyList<RingAccountRingtone>>(r => r.Count == response.Ringtones!.Count),
                It.Is<RingAccountRingtoneDefaults>(d => d.DefaultDingId == response.DefaultDingId && d.RingAccountId == AccountId),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, handler.CountRequests(RingtonesUrl));
        }

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_CapturedLocationData_WritesGroupsModeSettingsAndSharingForLocation()
        {
            var handler = CreateHandler();
            var writer = CreateWriter();

            _ = await SyncAsync(handler, writer, new RecordingLogger());

            GroupsResponse groups = JsonSerializer.Deserialize<GroupsResponse>(ReadFixture("light-groups-sanitized.json"))!;
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>(ReadFixture("location-mode-sanitized.json"))!;
            LocationModeSettingsResponse settings = JsonSerializer.Deserialize<LocationModeSettingsResponse>(ReadFixture("location-mode-settings-sanitized.json"))!;
            LocationModeSharingResponse sharing = JsonSerializer.Deserialize<LocationModeSharingResponse>(ReadFixture("location-mode-sharing-sanitized.json"))!;

            writer.Verify(w => w.ReplaceLightGroupSetAsync(
                It.Is<LightGroupSet>(s => s.ProviderLocationId == LocationId && s.IsOwner == groups.IsOwner),
                It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeAsync(
                It.Is<RingLocationMode>(m => m.ProviderLocationId == LocationId && m.Mode == mode.Mode),
                It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeSettingsAsync(
                It.Is<RingLocationModeSettings>(s => s.ProviderLocationId == LocationId && s.DefaultCameraOnlyMode == settings.Options!.DefaultCameraOnlyMode),
                It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeSharingAsync(
                It.Is<RingLocationModeSharing>(s => s.ProviderLocationId == LocationId && s.SharedUsersEnabled == sharing.SharedUsersEnabled),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_CachedSecondCall_ReusesLocationsButRefetchesAccountSettings()
        {
            // The location cache only skips the location/device list fetch; config sync runs on every call.
            // Throttling of account settings is the job of the config sync gate (IRingAccountSyncState), absent here.
            var handler = CreateHandler();
            var writer = CreateWriter();
            var logger = new RecordingLogger();
            RingDeviceDiscoveryService service = CreateService(handler, writer, logger, out Session session);
            _ = await session.Authenticate();

            _ = await service.GetLocationsAsync(AccountId);
            _ = await service.GetLocationsAsync(AccountId);

            Assert.Equal(1, handler.CountRequests(LocationsUrl));
            Assert.Equal(1, handler.CountRequests(RingDevicesUrl));
            Assert.Equal(2, handler.CountRequests(ProfileUrl));
            Assert.Equal(2, handler.CountRequests(RingtonesUrl));
            Assert.Equal(2, handler.CountRequests(GroupsUrl));
        }

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_GroupsFetchFails_LogsWarningAndStillWritesOtherFetches()
        {
            var handler = CreateHandler();
            handler.Routes[GroupsUrl] = (HttpStatusCode.InternalServerError, "{}");
            var writer = CreateWriter();
            var logger = new RecordingLogger();

            _ = await SyncAsync(handler, writer, logger);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            writer.Verify(w => w.ReplaceLightGroupSetAsync(It.IsAny<LightGroupSet>(), It.IsAny<CancellationToken>()), Times.Never);
            writer.Verify(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceRingtonesAsync(AccountId, It.IsAny<IReadOnlyList<RingAccountRingtone>>(), It.IsAny<RingAccountRingtoneDefaults>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeAsync(It.IsAny<RingLocationMode>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeSettingsAsync(It.IsAny<RingLocationModeSettings>(), It.IsAny<CancellationToken>()), Times.Once);
            writer.Verify(w => w.ReplaceLocationModeSharingAsync(It.IsAny<RingLocationModeSharing>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingAccountSettingsSync_GetLocationsAsync_LocationModeNotFound_LogsInformationAndSkipsModeData()
        {
            var handler = CreateHandler();
            handler.Routes.Remove(ModeUrl);
            var writer = CreateWriter();
            var logger = new RecordingLogger();

            _ = await SyncAsync(handler, writer, logger);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
            writer.Verify(w => w.ReplaceLocationModeAsync(It.IsAny<RingLocationMode>(), It.IsAny<CancellationToken>()), Times.Never);
            writer.Verify(w => w.ReplaceLightGroupSetAsync(It.IsAny<LightGroupSet>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Runs one sync for the account and returns the handler, so tests can count requests.</summary>
        private static async Task<IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>> SyncAsync(
            StubSessionHandler handler, Mock<IRingAccountSettingsWriter> writer, RecordingLogger logger)
        {
            RingDeviceDiscoveryService service = CreateService(handler, writer, logger, out Session session);
            _ = await session.Authenticate();
            return await service.GetLocationsAsync(AccountId);
        }

        private static RingDeviceDiscoveryService CreateService(
            StubSessionHandler handler, Mock<IRingAccountSettingsWriter> writer, RecordingLogger logger, out Session session)
        {
            session = new Session("user@example.com", "password", handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            return new RingDeviceDiscoveryService(
                logger,
                sessionProvider.Object,
                settingsPersister: new RingAccountSettingsPersister(writer.Object, logger));
        }

        /// <summary>Serves the sanitized captures for every account-settings URL, and a single location.</summary>
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

        /// <summary>
        /// Keeps each log entry's level and text so a test can assert a warning, an information entry, or no token text.
        /// Thread-safe and snapshot-based: the sync's fire-and-forget metadata task logs on this same instance.
        /// </summary>
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
