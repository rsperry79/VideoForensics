using Microsoft.Extensions.Logging;

using Moq;

using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using DbDevice = VideoForensics.Data.Common.Entities.Device;
using DbLocation = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Drives the three small config endpoints through the config sync over a stub HTTP session (no network): shared users
    /// per location, linked chime doorbots per chime, and snapshot timestamps per doorbot and stickup cam (one account-wide
    /// replace). They run only inside the config gate. A failing or null call logs a warning naming the location or device,
    /// never fails the sync, and leaves the rows it would have replaced untouched.
    /// </summary>
    public class RingSmallEndpointConfigSyncTests
    {
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private const string LocationName = "Home";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";
        private const string FrontDoorName = "Front Door";
        private const string GarageCamName = "Garage Cam";
        private const string ChimeName = "Hall Chime";
        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly Guid DbLocationId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        private static readonly Guid FrontDoorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid GarageCamGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        private static readonly Guid ChimeGuid = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
        private static readonly DateTime StartUtc = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);

        private const string ProfileUrl = "https://api.ring.com/clients_api/profile";
        private const string RingtonesUrl = "https://api.ring.com/clients_api/ringtones";
        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string GroupsUrl = $"https://api.ring.com/groups/v1/locations/{LocationId}/groups";
        private const string SharedUsersUrl = $"https://api.ring.com/clients_api/locations/{LocationId}/users";
        private const string ChimeLinkedUrl = "https://api.ring.com/clients_api/chimes/3003/linked_doorbots";
        private const string SnapshotUrl = "https://api.ring.com/clients_api/snapshots/timestamps";
        private const string FrontDoorSettingsUrl = "https://api.ring.com/devices/v1/devices/1001/settings";
        private const string GarageCamSettingsUrl = "https://api.ring.com/devices/v1/devices/2002/settings";

        [Fact]
        public async Task RingSmallEndpointConfigSync_ConfigDue_WritesSharedUsersForStoredLocation()
        {
            var rig = new SyncRig();

            await SyncAsync(rig);

            Assert.Equal(1, rig.Handler.CountRequests(SharedUsersUrl));
            rig.Writer.Verify(w => w.ReplaceLocationSharedUsersAsync(
                DbLocationId,
                It.Is<IReadOnlyList<RingLocationSharedUser>>(users => users.Count == 2
                    && users[0].ProviderUserId == 8001
                    && users[0].LocationId == DbLocationId
                    && users[0].Devices.Count == 1
                    && users[0].Devices[0].Permissions.Count == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_ConfigDue_WritesLinkedDoorbotsForStoredChime()
        {
            var rig = new SyncRig();

            await SyncAsync(rig);

            Assert.Equal(1, rig.Handler.CountRequests(ChimeLinkedUrl));
            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(
                ChimeGuid,
                It.Is<IReadOnlyList<RingChimeLinkedDoorbot>>(links => links.Count == 2
                    && links.Any(l => l.LinkedDoorbotId == 1001 && l.CallAlerts == true && l.MotionAlerts == false)
                    && links.Any(l => l.LinkedDoorbotId == 2002 && l.Kind == "stickup_cam" && l.MotionAlerts == true)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_ConfigDue_WritesOneAccountSnapshotReplaceWithEveryDoorbot()
        {
            var rig = new SyncRig();

            await SyncAsync(rig);

            // One request per doorbot and stickup cam; the chime is not a snapshot source.
            Assert.Equal(2, rig.Handler.CountRequests(SnapshotUrl));
            rig.Writer.Verify(w => w.ReplaceSnapshotTimestampsAsync(
                AccountId,
                It.Is<IReadOnlyList<RingSnapshotTimestamp>>(rows => rows.Count == 2
                    && rows.Any(r => r.DoorbotId == 1001 && r.TimestampEpoch == 1767225600000)
                    && rows.Any(r => r.DoorbotId == 2002)
                    && rows.All(r => r.RingAccountId == AccountId)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_SharedUsersForbidden_LogsWarningAndOtherWritesStillRun()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[SharedUsersUrl] = Respond(HttpStatusCode.Forbidden, "{}");

            await SyncAsync(rig);

            rig.Writer.Verify(w => w.ReplaceLocationSharedUsersAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingLocationSharedUser>>(), It.IsAny<CancellationToken>()), Times.Never);
            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(ChimeGuid, It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>()), Times.Once);
            rig.Writer.Verify(w => w.ReplaceSnapshotTimestampsAsync(AccountId, It.IsAny<IReadOnlyList<RingSnapshotTimestamp>>(), It.IsAny<CancellationToken>()), Times.Once);
            rig.Writer.Verify(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()), Times.Once);

            LogEntry warning = Assert.Single(rig.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("shared users", StringComparison.Ordinal));
            Assert.Contains(LocationName, warning.Message, StringComparison.Ordinal);
            Assert.False(warning.HasException);
            Assert.DoesNotContain(rig.Logger.Entries, e => e.Message.Contains(AccessToken, StringComparison.Ordinal));
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_LinkedDoorbotsNotFound_KeepsStoredRowsAndWarnsWithDeviceName()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[ChimeLinkedUrl] = Respond(HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");

            await SyncAsync(rig);

            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>()), Times.Never);
            LogEntry warning = Assert.Single(rig.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("linked chime", StringComparison.Ordinal));
            Assert.Contains(ChimeName, warning.Message, StringComparison.Ordinal);
            Assert.False(warning.HasException);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_OneSnapshotFails_LeavesAccountSnapshotRowsUntouched()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[SnapshotUrl] = body => SnapshotResponse(body, failingDoorbotId: 2002);

            await SyncAsync(rig);

            // The other doorbot's call still runs, but a partial set must not replace the account's rows.
            Assert.Equal(2, rig.Handler.CountRequests(SnapshotUrl));
            rig.Writer.Verify(w => w.ReplaceSnapshotTimestampsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingSnapshotTimestamp>>(), It.IsAny<CancellationToken>()), Times.Never);
            LogEntry warning = Assert.Single(rig.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(GarageCamName, StringComparison.Ordinal));
            Assert.Contains("snapshot", warning.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(warning.HasException);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_LinkedDoorbotsWithoutList_LeavesStoredRowsAndWarns()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[ChimeLinkedUrl] = Respond(HttpStatusCode.OK, "{}");

            await SyncAsync(rig);

            // A body with no linked_doorbots list is a null result, not an empty one: it must not clear the chime's rows.
            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Contains(rig.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(ChimeName, StringComparison.Ordinal));
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_EmptyLinkedDoorbotsList_ReplacesStoredRowsWithNone()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[ChimeLinkedUrl] = Respond(HttpStatusCode.OK, "{\"linked_doorbots\":[]}");

            await SyncAsync(rig);

            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(
                ChimeGuid,
                It.Is<IReadOnlyList<RingChimeLinkedDoorbot>>(links => links.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_EmptySnapshotTimestamps_ReplacesAccountRowsWithNone()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[SnapshotUrl] = Respond(HttpStatusCode.OK, "{\"timestamps\":[]}");

            await SyncAsync(rig);

            rig.Writer.Verify(w => w.ReplaceSnapshotTimestampsAsync(
                AccountId,
                It.Is<IReadOnlyList<RingSnapshotTimestamp>>(rows => rows.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_SnapshotBodyWithoutTimestamps_LeavesAccountRowsAndWarns()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[SnapshotUrl] = Respond(HttpStatusCode.OK, "{}");

            await SyncAsync(rig);

            rig.Writer.Verify(w => w.ReplaceSnapshotTimestampsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingSnapshotTimestamp>>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Contains(rig.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_ConfigWithinInterval_RunsNoSmallEndpointCalls()
        {
            var rig = new SyncRig();
            await SyncAsync(rig);

            rig.Clock.Advance(TimeSpan.FromHours(1));
            await SyncAsync(rig);

            Assert.Equal(1, rig.Handler.CountRequests(SharedUsersUrl));
            Assert.Equal(1, rig.Handler.CountRequests(ChimeLinkedUrl));
            Assert.Equal(2, rig.Handler.CountRequests(SnapshotUrl));
            rig.Writer.Verify(w => w.ReplaceLocationSharedUsersAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingLocationSharedUser>>(), It.IsAny<CancellationToken>()), Times.Once);
            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_ForcedConfigKindWithinInterval_RunsSmallEndpoints()
        {
            var rig = new SyncRig();
            await SyncAsync(rig);

            rig.Clock.Advance(TimeSpan.FromHours(1));
            rig.RunOptions.Setup(o => o.Current).Returns(new SyncRunOptions(Kind: SyncRunKind.Config));
            await SyncAsync(rig);

            Assert.Equal(2, rig.Handler.CountRequests(SharedUsersUrl));
            Assert.Equal(2, rig.Handler.CountRequests(ChimeLinkedUrl));
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_EventsOnlySync_RunsNoSmallEndpointCalls()
        {
            var rig = new SyncRig();
            rig.RunOptions.Setup(o => o.Current).Returns(new SyncRunOptions(Kind: SyncRunKind.Events));

            await SyncAsync(rig);

            Assert.Equal(0, rig.Handler.CountRequests(SharedUsersUrl));
            Assert.Equal(0, rig.Handler.CountRequests(ChimeLinkedUrl));
            Assert.Equal(0, rig.Handler.CountRequests(SnapshotUrl));
            rig.Writer.Verify(w => w.ReplaceLocationSharedUsersAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingLocationSharedUser>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_LocationRowNotStored_SkipsSharedUsersWithoutCallingRing()
        {
            var rig = new SyncRig();
            rig.Locations.Setup(r => r.GetByProviderLocationIdAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DbLocation?)null);

            await SyncAsync(rig);

            Assert.Equal(0, rig.Handler.CountRequests(SharedUsersUrl));
            rig.Writer.Verify(w => w.ReplaceLocationSharedUsersAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingLocationSharedUser>>(), It.IsAny<CancellationToken>()), Times.Never);
            rig.Writer.Verify(w => w.ReplaceChimeLinkedDoorbotsAsync(ChimeGuid, It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingSmallEndpointConfigSync_AllSmallEndpointsFail_ConfigGateStillMarksComplete()
        {
            var rig = new SyncRig();
            rig.Handler.Routes[SharedUsersUrl] = Respond(HttpStatusCode.InternalServerError, "{}");
            rig.Handler.Routes[ChimeLinkedUrl] = Respond(HttpStatusCode.InternalServerError, "{}");
            rig.Handler.Routes[SnapshotUrl] = Respond(HttpStatusCode.InternalServerError, "{}");

            await SyncAsync(rig);

            // The three endpoints are not part of the gate's completion test, so a persistent failure does not re-run config every sync.
            Assert.Equal(1, rig.State.MarkCount);
            Assert.Equal(StartUtc, rig.State.LastConfigSyncUtc);
        }

        private static async Task SyncAsync(SyncRig rig)
        {
            var session = new Session("user@example.com", "password", rig.Handler);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(AccountId)).Returns(session);
            var service = new RingDeviceDiscoveryService(
                rig.Logger,
                sessionProvider.Object,
                locationRepository: rig.Locations.Object,
                settingsPersister: new RingAccountSettingsPersister(rig.Writer.Object, rig.Logger),
                syncState: rig.State,
                timeProvider: rig.Clock,
                syncRunOptions: rig.RunOptions.Object,
                deviceSettingsPersister: new RingDeviceSettingsPersister(new Mock<IVideoForensicsDataClient>().Object, rig.Devices.Object, rig.Logger));
            _ = await session.Authenticate();
            _ = await service.GetLocationsAsync(AccountId);
        }

        private static Func<string, (HttpStatusCode Status, string Body)> Respond(HttpStatusCode status, string body)
        {
            return _ => (status, body);
        }

        /// <summary>Builds the snapshot response for the doorbot named in the request body, as Ring does: one entry for that doorbot.</summary>
        private static (HttpStatusCode Status, string Body) SnapshotResponse(string requestBody, long failingDoorbotId = -1)
        {
            long doorbotId = long.Parse(Regex.Match(requestBody, @"\[\s*(\d+)\s*\]").Groups[1].Value);
            if (doorbotId == failingDoorbotId)
            {
                return (HttpStatusCode.InternalServerError, "{}");
            }

            return (HttpStatusCode.OK, ReadFixture("snapshot-timestamps-sanitized.json").Replace("\"doorbot_id\": 1001", $"\"doorbot_id\": {doorbotId}", StringComparison.Ordinal));
        }

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        }

        /// <summary>Everything one sync needs, with the account, device list and small endpoints served by default.</summary>
        private sealed class SyncRig
        {
            public StubSessionHandler Handler { get; } = CreateHandler();

            public Mock<IRingAccountSettingsWriter> Writer { get; } = CreateWriter();

            public Mock<IDeviceRepository> Devices { get; } = CreateDeviceRepository();

            public Mock<ILocationRepository> Locations { get; } = CreateLocationRepository();

            public Mock<ISyncRunOptions> RunOptions { get; } = CreateRunOptions();

            public RecordingLogger Logger { get; } = new();

            public InMemorySyncState State { get; } = new();

            public ManualTimeProvider Clock { get; } = new(StartUtc);
        }

        private static StubSessionHandler CreateHandler()
        {
            var handler = new StubSessionHandler(AccessToken);
            handler.Routes[ProfileUrl] = Respond(HttpStatusCode.OK, ReadFixture("profile-sanitized.json"));
            handler.Routes[RingtonesUrl] = Respond(HttpStatusCode.OK, ReadFixture("ringtones-sanitized.json"));
            handler.Routes[LocationsUrl] = Respond(HttpStatusCode.OK, $"{{\"user_locations\":[{{\"location_id\":\"{LocationId}\",\"name\":\"{LocationName}\",\"is_owner\":true}}]}}");
            handler.Routes[RingDevicesUrl] = Respond(HttpStatusCode.OK,
                "{\"doorbots\":[" +
                $"{{\"id\":1001,\"description\":\"{FrontDoorName}\",\"location_id\":\"{LocationId}\",\"device_id\":\"front\"}}" +
                "],\"authorized_doorbots\":[]," +
                "\"stickup_cams\":[" +
                $"{{\"id\":2002,\"description\":\"{GarageCamName}\",\"location_id\":\"{LocationId}\",\"device_id\":\"garage\"}}" +
                "],\"base_stations\":[]," +
                "\"chimes\":[" +
                $"{{\"id\":3003,\"description\":\"{ChimeName}\",\"location_id\":\"{LocationId}\",\"device_id\":\"hall\"}}" +
                "]}");
            handler.Routes[GroupsUrl] = Respond(HttpStatusCode.OK, ReadFixture("light-groups-sanitized.json"));
            handler.Routes[SharedUsersUrl] = Respond(HttpStatusCode.OK, ReadFixture("shared-users-sanitized.json"));
            handler.Routes[ChimeLinkedUrl] = Respond(HttpStatusCode.OK, ReadFixture("linked-chime-doorbots-sanitized.json"));
            handler.Routes[SnapshotUrl] = body => SnapshotResponse(body);
            handler.Routes[FrontDoorSettingsUrl] = Respond(HttpStatusCode.OK, "{}");
            handler.Routes[GarageCamSettingsUrl] = Respond(HttpStatusCode.OK, "{}");
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
            _ = writer.Setup(w => w.ReplaceAccountMonitoringStatusAsync(It.IsAny<RingAccountMonitoringStatus>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceLocationSharedUsersAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingLocationSharedUser>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceChimeLinkedDoorbotsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingChimeLinkedDoorbot>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = writer.Setup(w => w.ReplaceSnapshotTimestampsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RingSnapshotTimestamp>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return writer;
        }

        /// <summary>Lists the front door, the garage cam and the hall chime as stored rows. The chime is stored so its linked doorbots are written.</summary>
        private static Mock<IDeviceRepository> CreateDeviceRepository()
        {
            var rows = new List<DbDevice>
            {
                new() { Id = FrontDoorGuid, ProviderDeviceId = "1001", Name = FrontDoorName, Type = "doorbot", LocationId = DbLocationId },
                new() { Id = GarageCamGuid, ProviderDeviceId = "2002", Name = GarageCamName, Type = "stickup_cam", LocationId = DbLocationId },
                new() { Id = ChimeGuid, ProviderDeviceId = "3003", Name = ChimeName, Type = "chime", LocationId = DbLocationId },
            };

            var repository = new Mock<IDeviceRepository>();
            _ = repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IReadOnlyList<DbDevice>>(rows));
            return repository;
        }

        private static Mock<ILocationRepository> CreateLocationRepository()
        {
            var repository = new Mock<ILocationRepository>();
            _ = repository.Setup(r => r.GetByProviderLocationIdAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DbLocation { Id = DbLocationId, ProviderLocationId = LocationId, Name = LocationName });
            return repository;
        }

        private static Mock<ISyncRunOptions> CreateRunOptions()
        {
            var options = new Mock<ISyncRunOptions>();
            _ = options.Setup(o => o.Current).Returns(new SyncRunOptions());
            return options;
        }

        private sealed class StubSessionHandler : HttpMessageHandler
        {
            private readonly string _accessToken;
            private readonly List<string> _requestedUrls = [];

            public StubSessionHandler(string accessToken)
            {
                _accessToken = accessToken;
            }

            /// <summary>Each route sees the request body, so a route can answer per doorbot.</summary>
            public Dictionary<string, Func<string, (HttpStatusCode Status, string Body)>> Routes { get; } = [];

            public int CountRequests(string url)
            {
                return _requestedUrls.Count(u => u == url);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;
                _requestedUrls.Add(url);

                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    string token = $"{{\"access_token\":\"{_accessToken}\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(token), RequestMessage = request };
                }

                string requestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                (HttpStatusCode status, string body) = Routes.TryGetValue(url, out Func<string, (HttpStatusCode Status, string Body)>? route)
                    ? route(requestBody)
                    : (HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");

                return new HttpResponseMessage(status) { Content = new StringContent(body), RequestMessage = request };
            }
        }

        /// <summary>In-memory gate state; the last config run is recorded when a config sync completes.</summary>
        private sealed class InMemorySyncState : IRingAccountSyncState
        {
            public int IntervalHours { get; set; } = 24;

            public DateTime? LastConfigSyncUtc { get; private set; }

            public int MarkCount { get; private set; }

            public Task<RingConfigSyncState> GetConfigSyncStateAsync(Guid providerAccountId, CancellationToken ct)
            {
                return Task.FromResult(new RingConfigSyncState(IntervalHours, LastConfigSyncUtc));
            }

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

            public ManualTimeProvider(DateTime startUtc)
            {
                _now = new DateTimeOffset(startUtc, TimeSpan.Zero);
            }

            public void Advance(TimeSpan by)
            {
                _now = _now.Add(by);
            }

            public override DateTimeOffset GetUtcNow()
            {
                return _now;
            }
        }

        private sealed record LogEntry(LogLevel Level, string Message, bool HasException);

        /// <summary>Keeps each entry's level, text and whether an exception object was passed. Thread-safe for the fire-and-forget metadata task.</summary>
        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<LogEntry> _entries = new();

            public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

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
                _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception != null));
            }
        }
    }
}
