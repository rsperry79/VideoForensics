using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Logging;
using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Drives the config sync's device-settings step through a stub HTTP session (no network). Device settings are fetched
    /// and written only when the config gate is due, once per doorbot and stickup cam; chimes are never asked for settings.
    /// </summary>
    public class RingDeviceSettingsConfigSyncTests
    {
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly DateTime StartUtc = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        private static readonly Guid FrontDoorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid BackDoorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        private static readonly Guid GarageCamGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

        private const string ProfileUrl = "https://api.ring.com/clients_api/profile";
        private const string RingtonesUrl = "https://api.ring.com/clients_api/ringtones";
        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string FrontDoorSettingsUrl = "https://api.ring.com/devices/v1/devices/1001/settings";
        private const string BackDoorSettingsUrl = "https://api.ring.com/devices/v1/devices/1002/settings";
        private const string GarageCamSettingsUrl = "https://api.ring.com/devices/v1/devices/2002/settings";
        private const string ChimeSettingsUrl = "https://api.ring.com/devices/v1/devices/3003/settings";

        private const string BackDoorSettingsJson =
            "{\"doorbell_volume\":3,\"zone_settings\":{\"motion\":[{\"id\":\"z1\",\"name\":\"Porch\",\"state\":1," +
            "\"properties\":{\"detection_types\":[\"motion\"],\"style\":\"solid\"},\"vertices\":[{\"x\":0,\"y\":0}]}]}}";

        private const string GarageCamSettingsJson =
            "{\"pir_settings\":{\"sensitivity_1\":1,\"sensitivity_2\":2,\"sensitivity_3\":3,\"zone_enable\":1,\"zone_mask\":15}," +
            "\"zone_settings\":{\"motion\":[]}}";

        [Fact]
        public async Task RingDeviceSettingsConfigSync_ConfigDue_WritesSettingsAndZonesOncePerDevice()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), new RecordingLogger());

            Assert.Equal(1, handler.CountRequests(FrontDoorSettingsUrl));
            Assert.Equal(1, handler.CountRequests(BackDoorSettingsUrl));
            Assert.Equal(1, handler.CountRequests(GarageCamSettingsUrl));

            Assert.Equal(3, writes.Count);
            DeviceWrite frontDoor = Assert.Single(writes, w => w.DeviceId == FrontDoorGuid);
            Assert.NotNull(frontDoor.Settings);
            // The captured doorbot fixture carries two motion zones and no package zones.
            Assert.Equal(2, frontDoor.Settings!.Zones!.Count);
            Assert.All(frontDoor.Settings.Zones, z => Assert.Equal("motion", z.Source));

            DeviceWrite backDoor = Assert.Single(writes, w => w.DeviceId == BackDoorGuid);
            Assert.Single(backDoor.Settings!.Zones!);

            DeviceWrite garageCam = Assert.Single(writes, w => w.DeviceId == GarageCamGuid);
            Assert.NotNull(garageCam.Settings);
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_ConfigWithinInterval_DoesNotCallGetDeviceSettings()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            var state = new InMemorySyncState();
            var clock = new ManualTimeProvider(StartUtc);
            _ = await SyncAsync(handler, data, state, clock, new RecordingLogger());

            clock.Advance(TimeSpan.FromHours(1));
            _ = await SyncAsync(handler, data, state, clock, new RecordingLogger());

            Assert.Equal(1, handler.CountRequests(FrontDoorSettingsUrl));
            Assert.Equal(1, handler.CountRequests(BackDoorSettingsUrl));
            Assert.Equal(1, handler.CountRequests(GarageCamSettingsUrl));
            Assert.Equal(3, writes.Count);
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_OneDeviceFails_OtherDevicesStillWritten()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            handler.Routes[BackDoorSettingsUrl] = (HttpStatusCode.InternalServerError, "{}");
            var logger = new RecordingLogger();

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), logger);

            Assert.Contains(writes, w => w.DeviceId == FrontDoorGuid);
            Assert.Contains(writes, w => w.DeviceId == GarageCamGuid);
            Assert.DoesNotContain(writes, w => w.DeviceId == BackDoorGuid);

            (LogLevel Level, string Message) warning = Assert.Single(logger.Entries, e => e.Message.Contains("Back Door", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.DoesNotContain("api.ring.com", warning.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("devices/1002", warning.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(AccessToken, warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_DeviceSettingsNotFound_LogsInformationAndSkipsDevice()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            handler.Routes[GarageCamSettingsUrl] = (HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");
            var logger = new RecordingLogger();

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), logger);

            Assert.DoesNotContain(writes, w => w.DeviceId == GarageCamGuid);
            Assert.Contains(writes, w => w.DeviceId == FrontDoorGuid);
            Assert.Contains(writes, w => w.DeviceId == BackDoorGuid);
            (LogLevel Level, string Message) info = Assert.Single(logger.Entries, e => e.Message.Contains("Garage Cam", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Information, info.Level);
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_ChimePresent_ChimeSettingsNotRequested()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            handler.Routes[ChimeSettingsUrl] = (HttpStatusCode.OK, "{}");

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), new RecordingLogger());

            Assert.Equal(0, handler.CountRequests(ChimeSettingsUrl));
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_DeviceMissingFromDatabase_SkipsWriteWithoutFailing()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            var logger = new RecordingLogger();
            var tally = new SyncRunTally();
            var devices = CreateDeviceRepository(includeBackDoor: false);

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), logger, tally, devices);

            Assert.DoesNotContain(writes, w => w.DeviceId == BackDoorGuid);
            Assert.Equal(2, writes.Count);
            Assert.Equal(1, handler.CountRequests(BackDoorSettingsUrl));
            // Account config areas (profile, ringtones) plus the two stored devices.
            Assert.Equal(4, tally.Snapshot().ConfigAreasSynced);
        }

        [Fact]
        public async Task RingDeviceSettingsConfigSync_ConfigRun_TallyCountsStoredDevices()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            var tally = new SyncRunTally();

            _ = await SyncAsync(handler, data, new InMemorySyncState(), new ManualTimeProvider(StartUtc), new RecordingLogger(), tally);

            // Profile and ringtones (2 account areas) plus the three stored devices.
            SyncRunTotals totals = tally.Snapshot();
            Assert.True(totals.ConfigRan);
            Assert.Equal(5, totals.ConfigAreasSynced);
        }

        [Fact]
        public async Task RingDeviceDiscoveryService_ConfigSync_SkippedDeviceSettings_DoesNotMarkConfigComplete()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            var state = new InMemorySyncState();
            var logger = new RecordingLogger();
            var devices = CreateDeviceRepository(includeBackDoor: false);

            _ = await SyncAsync(handler, data, state, new ManualTimeProvider(StartUtc), logger, devices: devices);

            // The back door row does not exist yet, so its settings were skipped and the gate must stay open for a retry.
            Assert.DoesNotContain(writes, w => w.DeviceId == BackDoorGuid);
            Assert.Equal(0, state.MarkCount);
            Assert.Null(state.LastConfigSyncUtc);
            (LogLevel Level, string Message) skipped = Assert.Single(logger.Entries, e => e.Message.Contains("Back Door", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Information, skipped.Level);
        }

        [Fact]
        public async Task RingDeviceDiscoveryService_ConfigSync_AllDeviceSettingsStored_MarksConfigComplete()
        {
            var writes = new ConcurrentBag<DeviceWrite>();
            var data = CreateDataClient(writes);
            var handler = CreateHandler();
            var state = new InMemorySyncState();

            _ = await SyncAsync(handler, data, state, new ManualTimeProvider(StartUtc), new RecordingLogger());

            Assert.Equal(1, state.MarkCount);
            Assert.Equal(StartUtc, state.LastConfigSyncUtc);
        }

        private static async Task<IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>> SyncAsync(
            StubSessionHandler handler,
            Mock<IVideoForensicsDataClient> data,
            InMemorySyncState state,
            ManualTimeProvider clock,
            RecordingLogger logger,
            SyncRunTally? tally = null,
            Mock<IDeviceRepository>? devices = null)
        {
            var session = new Session("user@example.com", "password", handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            var service = new RingDeviceDiscoveryService(
                logger,
                sessionProvider.Object,
                settingsPersister: new RingAccountSettingsPersister(new Mock<IRingAccountSettingsWriter>().Object, logger),
                eventHistoryWriter: new Mock<IRingEventHistoryWriter>().Object,
                syncState: state,
                timeProvider: clock,
                syncRunTally: tally,
                deviceSettingsPersister: new RingDeviceSettingsPersister(data.Object, (devices ?? CreateDeviceRepository(true)).Object, logger));
            _ = await session.Authenticate();
            return await service.GetLocationsAsync(AccountId);
        }

        /// <summary>Records every device-details write so tests can check which device got which settings.</summary>
        private static Mock<IVideoForensicsDataClient> CreateDataClient(ConcurrentBag<DeviceWrite> writes)
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.RecordDeviceDetailsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<DeviceCapabilities?>(),
                    It.IsAny<DeviceFeatures?>(),
                    It.IsAny<IReadOnlyList<DeviceAlerts>?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<DeviceSettingsChildren?>()))
                .Callback<Guid, Guid?, DeviceCapabilities?, DeviceFeatures?, IReadOnlyList<DeviceAlerts>?, CancellationToken, DeviceSettingsChildren?>(
                    (deviceId, _, _, _, _, _, settings) => writes.Add(new DeviceWrite(deviceId, settings)))
                .Returns(Task.CompletedTask);
            return data;
        }

        /// <summary>Lists the three stored devices by their provider ids. The back door is left out when <paramref name="includeBackDoor"/> is false.</summary>
        private static Mock<IDeviceRepository> CreateDeviceRepository(bool includeBackDoor)
        {
            var rows = new List<Device>
            {
                new() { Id = FrontDoorGuid, ProviderDeviceId = "1001", Name = "Front Door", Type = "doorbot", LocationId = Guid.NewGuid() },
                new() { Id = GarageCamGuid, ProviderDeviceId = "2002", Name = "Garage Cam", Type = "stickup_cam", LocationId = Guid.NewGuid() }
            };
            if (includeBackDoor)
            {
                rows.Add(new Device { Id = BackDoorGuid, ProviderDeviceId = "1002", Name = "Back Door", Type = "doorbot", LocationId = Guid.NewGuid() });
            }

            var repository = new Mock<IDeviceRepository>();
            _ = repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IReadOnlyList<Device>>(rows));
            return repository;
        }

        /// <summary>Serves the account config, the device list and the three device settings objects; anything else is a 404.</summary>
        private static StubSessionHandler CreateHandler()
        {
            var handler = new StubSessionHandler(AccessToken);
            handler.Routes[ProfileUrl] = (HttpStatusCode.OK, ReadFixture("profile-sanitized.json"));
            handler.Routes[RingtonesUrl] = (HttpStatusCode.OK, ReadFixture("ringtones-sanitized.json"));
            handler.Routes[LocationsUrl] = (HttpStatusCode.OK, $"{{\"user_locations\":[{{\"location_id\":\"{LocationId}\",\"name\":\"Home\",\"is_owner\":true}}]}}");
            handler.Routes[RingDevicesUrl] = (HttpStatusCode.OK,
                "{\"doorbots\":[" +
                $"{{\"id\":1001,\"description\":\"Front Door\",\"location_id\":\"{LocationId}\",\"device_id\":\"front\"}}," +
                $"{{\"id\":1002,\"description\":\"Back Door\",\"location_id\":\"{LocationId}\",\"device_id\":\"back\"}}" +
                "],\"authorized_doorbots\":[]," +
                "\"stickup_cams\":[" +
                $"{{\"id\":2002,\"description\":\"Garage Cam\",\"location_id\":\"{LocationId}\",\"device_id\":\"garage\"}}" +
                "],\"base_stations\":[]," +
                "\"chimes\":[" +
                $"{{\"id\":3003,\"description\":\"Hall Chime\",\"location_id\":\"{LocationId}\",\"device_id\":\"hall\"}}" +
                "]}");
            handler.Routes[FrontDoorSettingsUrl] = (HttpStatusCode.OK, ReadFixture("doorbot-settings-sanitized.json"));
            handler.Routes[BackDoorSettingsUrl] = (HttpStatusCode.OK, BackDoorSettingsJson);
            handler.Routes[GarageCamSettingsUrl] = (HttpStatusCode.OK, GarageCamSettingsJson);
            return handler;
        }

        private static string ReadFixture(string name) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

        private sealed record DeviceWrite(Guid DeviceId, DeviceSettingsChildren? Settings);

        /// <summary>In-memory gate state; the last config run is recorded when a config sync completes.</summary>
        private sealed class InMemorySyncState : IRingAccountSyncState
        {
            public int IntervalHours { get; set; } = 24;

            public DateTime? LastConfigSyncUtc { get; private set; }

            public int MarkCount { get; private set; }

            public Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct) =>
                Task.FromResult(new RingConfigSyncState(IntervalHours, LastConfigSyncUtc));

            public Task MarkConfigSyncCompletedAsync(Guid providerAccountId, DateTime utcNow, CancellationToken ct)
            {
                LastConfigSyncUtc = utcNow;
                MarkCount++;
                return Task.CompletedTask;
            }

            public Task<bool> SetConfigIntervalHoursAsync(Guid providerAccountId, int hours, CancellationToken ct)
            {
                IntervalHours = hours;
                return Task.FromResult(true);
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
