using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Round-trips the video search, location event, location history, doorbot history and app location history
    /// graphs through SQLite, and checks that a second write for the same event replaces every child row.
    /// Values are the sanitized captured payloads (no personal data).
    /// </summary>
    public class RingEventHistoryPersistenceTests : IAsyncLifetime
    {
        private const string LocationId = "11111111-2222-3333-4444-555555555555";
        private SqliteInMemoryFixture _fixture = null!;
        private RingEventHistoryWriter _writer = null!;
        private readonly Guid _ringAccountId = Guid.Parse("22222222-3333-4444-5555-666666666666");

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _writer = new RingEventHistoryWriter(_fixture.Factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static RingVideoSearchEvent BuildVideoSearch(string dingId, string kind)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingItemId = 7,
                ProviderEventId = dingId,
                CreatedAtMs = 1792178873638,
                UpdatedAtMs = 1792178914777000,
                Kind = kind,
                State = "timed_out",
                DurationSeconds = 41,
                Favorite = false,
                HqUrl = "https://sanitized.example.invalid/hq",
                LqUrl = "https://sanitized.example.invalid/lq",
                ThumbnailUrl = "https://sanitized.example.invalid/thumb",
                UntranscodedUrl = "https://sanitized.example.invalid/raw",
                IsE2ee = false,
                PrerollDuration = 7400,
                HadSubscription = true,
                OwnerId = "00000000",
                SourceId = "86401464",
                Riid = "riid-1",
                BatteryPercentage = "90",
                BatteryStatus = "full",
            };
        }

        private static RingEventDetails BuildVideoSearchDetails(Guid rowId)
        {
            return new()
            {
                Doorbot = new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = rowId, DoorbotId = 86401464, Description = "Sanitized description", Type = "doorbell" },
                Properties = new RingEventProperties { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = rowId, IsAlexa = false, IsSidewalk = false, IsAutoreply = false, StarkReviewed = false, PackagePickupDetected = true, DetectionConfidence = 0.75 },
                Geolocation = new RingEventGeolocation { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = rowId, Latitude = 1.5, Longitude = -2.5, Accuracy = 10, TimestampMs = 1792178873000, LocationSource = "gps" },
                SubscriptionReceipt = new RingEventSubscriptionReceipt { Id = Guid.NewGuid(), Source = RingEventSources.VideoSearch, SourceRowId = rowId, SubscriptionId = "sub-1", SubscriptionType = "basic", CapturedAt = 1, ExpiresAt = 2 },
                CvProperties = new RingEventCvProperties
                {
                    Id = Guid.NewGuid(),
                    Source = RingEventSources.VideoSearch,
                    SourceRowId = rowId,
                    PersonDetected = true,
                    StreamBroken = false,
                    DetectionType = "human",
                    FullDescription = "full",
                    ShortDescription = "short",
                    Similarity = 0.5,
                    Anomaly = 0.25,
                    DetectionTypes =
                [
                    new RingEventCvDetectionType
                    {
                        Id = Guid.NewGuid(), Position = 0, DetectionType = "human",
                        VerifiedTimestamps = [new RingEventCvVerifiedTimestamp { Id = Guid.NewGuid(), Position = 0, TimestampMs = 1792178874177 }],
                    },
                ],
                    SecurityAlerts = [new RingEventCvSecurityAlert { Id = Guid.NewGuid(), Severity = "high", Alerts = [new RingEventCvSecurityAlertItem { Id = Guid.NewGuid(), Position = 0, AlertText = "alert" }] }],
                    Profiles = [new RingEventCvProfile { Id = Guid.NewGuid(), Position = 0, ProfileId = "p-1", Name = "Profile", Confidence = 0.9, ThumbnailUrl = "https://sanitized.example.invalid/profile" }],
                    DetectionDetails = [new RingEventCvDetectionDetail { Id = Guid.NewGuid(), Position = 0, TimestampMs = 1792178874177, Type = "human", Confidence = 0.8, ModelVersion = "v2", Zones = [new RingEventCvDetectionZone { Id = Guid.NewGuid(), Position = 0, ZoneId = "z-1", Name = "Porch", Confidence = 0.7 }] }],
                    Tags = [new RingEventCvTag { Id = Guid.NewGuid(), Position = 0, Tag = "porch" }],
                },
            };
        }

        [Fact]
        public async Task RingEventHistoryWriter_VideoSearchRoundTrip_PersistsRootAndEveryNestedRow()
        {
            RingVideoSearchEvent evt = BuildVideoSearch("7697349643908571576", "motion");

            await _writer.ReplaceVideoSearchEventAsync(_ringAccountId, evt, BuildVideoSearchDetails(evt.Id), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            RingVideoSearchEvent stored = await db.RingVideoSearchEvents.SingleAsync();
            Assert.Equal("motion", stored.Kind);
            Assert.Equal(7400, stored.PrerollDuration);
            Assert.Equal("90", stored.BatteryPercentage);
            Assert.Equal(_ringAccountId, stored.RingAccountId);

            RingEventDoorbot doorbot = await db.RingEventDoorbots.SingleAsync(d => d.SourceRowId == evt.Id);
            Assert.Equal(86401464, doorbot.DoorbotId);
            Assert.Equal("doorbell", doorbot.Type);
            Assert.True((await db.RingEventProperties.SingleAsync(p => p.SourceRowId == evt.Id)).PackagePickupDetected);
            Assert.Equal("gps", (await db.RingEventGeolocations.SingleAsync(g => g.SourceRowId == evt.Id)).LocationSource);
            Assert.Equal("sub-1", (await db.RingEventSubscriptionReceipts.SingleAsync(r => r.SourceRowId == evt.Id)).SubscriptionId);
            Assert.Empty(await db.RingEventDevicePlacements.Where(p => p.SourceRowId == evt.Id).ToListAsync());

            RingEventCvProperties cv = await db.RingEventCvProperties.Include(c => c.DetectionTypes).ThenInclude(t => t.VerifiedTimestamps)
                .Include(c => c.SecurityAlerts).ThenInclude(s => s.Alerts)
                .Include(c => c.Profiles).Include(c => c.DetectionDetails).ThenInclude(d => d.Zones).Include(c => c.Tags)
                .SingleAsync(c => c.SourceRowId == evt.Id);
            Assert.Equal("human", cv.DetectionTypes.Single().DetectionType);
            Assert.Equal(1792178874177, cv.DetectionTypes.Single().VerifiedTimestamps.Single().TimestampMs);
            Assert.Equal("alert", cv.SecurityAlerts.Single().Alerts.Single().AlertText);
            Assert.Equal("p-1", cv.Profiles.Single().ProfileId);
            Assert.Equal("Porch", cv.DetectionDetails.Single().Zones.Single().Name);
            Assert.Equal("porch", cv.Tags.Single().Tag);
        }

        [Fact]
        public async Task RingEventHistoryWriter_VideoSearchReplace_RemovesPreviousNestedRows()
        {
            RingVideoSearchEvent first = BuildVideoSearch("ding-1", "motion");
            await _writer.ReplaceVideoSearchEventAsync(_ringAccountId, first, BuildVideoSearchDetails(first.Id), CancellationToken.None);

            RingVideoSearchEvent second = BuildVideoSearch("ding-1", "on_demand");
            await _writer.ReplaceVideoSearchEventAsync(_ringAccountId, second, new RingEventDetails(), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            RingVideoSearchEvent stored = await db.RingVideoSearchEvents.SingleAsync();
            Assert.Equal("on_demand", stored.Kind);
            Assert.Empty(await db.RingEventDoorbots.ToListAsync());
            Assert.Empty(await db.RingEventCvProperties.ToListAsync());
            Assert.Empty(await db.RingEventCvDetectionTypes.ToListAsync());
            Assert.Empty(await db.RingEventCvVerifiedTimestamps.ToListAsync());
            Assert.Empty(await db.RingEventCvSecurityAlertItems.ToListAsync());
            Assert.Empty(await db.RingEventCvDetectionZones.ToListAsync());
            Assert.Empty(await db.RingEventCvTags.ToListAsync());
        }

        [Fact]
        public async Task RingEventHistoryWriter_LocationEventRoundTrip_PersistsPageAndEvent()
        {
            await _writer.ReplaceLocationEventPageAsync(new RingLocationEventPage { Id = Guid.NewGuid(), ProviderLocationId = LocationId, PaginationKey = "sanitized-pagination-key" }, CancellationToken.None);
            var evt = new RingLocationEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                ProviderEventId = "7697349643908571576",
                SourceId = "86401464",
                EventType = "motion",
                State = "timed_out",
                Favorite = false,
                Recorded = true,
                RecordingStatus = "ready",
                IsE2ee = false,
                CreatedAt = "2026-10-16T19:27:46Z",
                HadSubscription = true,
                OwnerId = "00000000",
                Riid = "riid-2",
                DoorbotId = 86401464,
                DingId = 7697349643908571000,
                DingIdString = "7697349643908571576",
                Kind = "motion",
                BatteryPercentage = "90",
                BatteryStatus = "full",
            };
            var details = new RingEventDetails
            {
                Doorbot = new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.LocationEvent, SourceRowId = evt.Id, DoorbotId = 86401464, Description = "Sanitized description", Type = "doorbell" },
                LastLocation = new RingEventLastLocation { Id = Guid.NewGuid(), Source = RingEventSources.LocationEvent, SourceRowId = evt.Id, Latitude = 3, Longitude = 4, Accuracy = 5, TimestampMs = 6, Name = "Last" },
                Siren = new RingEventSiren { Id = Guid.NewGuid(), Source = RingEventSources.LocationEvent, SourceRowId = evt.Id, Armed = true, TriggeredAt = 9, TriggerType = "motion", AlarmState = "off" },
                CvProperties = new RingEventCvProperties { Id = Guid.NewGuid(), Source = RingEventSources.LocationEvent, SourceRowId = evt.Id, DetectionType = "human" },
            };

            await _writer.ReplaceLocationEventAsync(evt, details, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("sanitized-pagination-key", (await db.RingLocationEventPages.SingleAsync()).PaginationKey);
            RingLocationEvent stored = await db.RingLocationEvents.SingleAsync();
            Assert.Equal(7697349643908571000, stored.DingId);
            Assert.Equal("ready", stored.RecordingStatus);
            Assert.Equal(9, (await db.RingEventSirens.SingleAsync(s => s.SourceRowId == evt.Id)).TriggeredAt);
            Assert.Equal("Last", (await db.RingEventLastLocations.SingleAsync(l => l.SourceRowId == evt.Id)).Name);
            Assert.Equal("human", (await db.RingEventCvProperties.SingleAsync(c => c.SourceRowId == evt.Id)).DetectionType);
        }

        [Fact]
        public async Task RingEventHistoryWriter_LocationEventReplace_RemovesPreviousNestedRows()
        {
            var first = new RingLocationEvent { Id = Guid.NewGuid(), ProviderLocationId = LocationId, ProviderEventId = "evt-1", EventType = "motion" };
            await _writer.ReplaceLocationEventAsync(first, new RingEventDetails { Siren = new RingEventSiren { Id = Guid.NewGuid(), Source = RingEventSources.LocationEvent, SourceRowId = first.Id, Armed = true } }, CancellationToken.None);

            var second = new RingLocationEvent { Id = Guid.NewGuid(), ProviderLocationId = LocationId, ProviderEventId = "evt-1", EventType = "on_demand" };
            await _writer.ReplaceLocationEventAsync(second, new RingEventDetails(), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("on_demand", (await db.RingLocationEvents.SingleAsync()).EventType);
            Assert.Empty(await db.RingEventSirens.ToListAsync());
        }

        [Fact]
        public async Task RingEventHistoryWriter_LocationHistoryRoundTrip_PersistsVisualizationMediaMetadataAndTracks()
        {
            await _writer.ReplaceLocationHistoryPageAsync(new RingLocationHistoryPage { Id = Guid.NewGuid(), ProviderLocationId = LocationId, PaginationKey = "sanitized-pagination-key" }, CancellationToken.None);
            RingLocationHistoryEvent evt = BuildLocationHistory("ding-h1", "motion");
            await _writer.ReplaceLocationHistoryEventAsync(evt, BuildLocationHistoryDetails(evt.Id), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("sanitized-pagination-key", (await db.RingLocationHistoryPages.SingleAsync()).PaginationKey);
            RingLocationHistoryEvent stored = await db.RingLocationHistoryEvents.SingleAsync();
            Assert.Equal(48231, stored.DurationMs);
            Assert.False(stored.IsWatched);
            Assert.Equal("Event", stored.Schema);
            RingLocationHistoryVisualization cloud = await db.RingLocationHistoryVisualizations.SingleAsync(v => v.RingLocationHistoryEventId == evt.Id && v.Kind == "cloud");
            Assert.Equal("cloud", cloud.Kind);
            RingLocationHistoryMedia media = await db.RingLocationHistoryMedia.SingleAsync(m => m.RingLocationHistoryVisualizationId == cloud.Id);
            Assert.Equal(7400, media.PrerollDurationMs);
            RingLocationHistoryMediaMetadata metadata = await db.RingLocationHistoryMediaMetadata.SingleAsync(m => m.RingLocationHistoryMediaId == media.Id);
            Assert.Equal("video.2.256", (await db.RingLocationHistoryMediaTracks.SingleAsync(t => t.RingLocationHistoryMediaMetadataId == metadata.Id && t.Type == "video")).TrackId);
            Assert.Equal(2, await db.RingLocationHistoryMediaTracks.CountAsync(t => t.RingLocationHistoryMediaMetadataId == metadata.Id));
            Assert.Equal("H264", metadata.VideoEncoding);
        }

        [Fact]
        public async Task RingEventHistoryWriter_LocationHistoryReplace_RemovesPreviousVisualizationsAndMedia()
        {
            RingLocationHistoryEvent first = BuildLocationHistory("ding-h2", "motion");
            await _writer.ReplaceLocationHistoryEventAsync(first, BuildLocationHistoryDetails(first.Id), CancellationToken.None);

            RingLocationHistoryEvent second = BuildLocationHistory("ding-h2", "on_demand");
            await _writer.ReplaceLocationHistoryEventAsync(second, new RingEventDetails(), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("on_demand", (await db.RingLocationHistoryEvents.SingleAsync()).EventType);
            Assert.Equal(2, await db.RingLocationHistoryVisualizations.CountAsync());
            Assert.Equal(1, await db.RingLocationHistoryMedia.CountAsync());
            Assert.Equal(1, await db.RingLocationHistoryMediaMetadata.CountAsync());
            Assert.Equal(2, await db.RingLocationHistoryMediaTracks.CountAsync());
        }

        private static RingLocationHistoryEvent BuildLocationHistory(string dingId, string eventType)
        {
            var evt = new RingLocationHistoryEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                ProviderEventId = dingId,
                Schema = "Event",
                SourceId = "86401464",
                StartTime = "2026-10-16T19:27:46.238Z",
                EndTime = "2026-10-16T19:28:34.469Z",
                EventType = eventType,
                DurationMs = 48231,
                SessionDurationMs = 41000,
                State = "timed_out",
                HadSubscription = true,
                IsFavorite = false,
                RecordingStatus = "ready",
                UpdatedAt = "2026-10-16T19:28:34.777Z",
                OwnerId = "00000000",
                DeleteInProgress = false,
                IsWatched = false,
                IsActive = true,
                UserActionDisabled = false,
                Riid = "riid-3",
            };
            var media = new RingLocationHistoryMedia
            {
                Id = Guid.NewGuid(),
                Schema = "Media",
                Url = "https://sanitized.example.invalid/media",
                StartTime = "2026-10-16T19:27:46.238Z",
                EndTime = "2026-10-16T19:28:34.469Z",
                IsE2ee = false,
                FileType = "VIDEO",
                FileFamily = "VIDEO",
                PrerollDurationMs = 7400,
                PlaybackDuration = 48231,
                Source = "Apsara",
                Metadata = new RingLocationHistoryMediaMetadata
                {
                    Id = Guid.NewGuid(),
                    Recording24x7Enabled = false,
                    DingId = dingId,
                    AudioEncoding = "PCMU",
                    VideoEncoding = "H264",
                    TbaTranscodingEnabled = true,
                    Tracks =
                    [
                        new RingLocationHistoryMediaTrack { Id = Guid.NewGuid(), Position = 0, TrackId = "audio.2.0", Codec = "PCMU", Type = "audio" },
                        new RingLocationHistoryMediaTrack { Id = Guid.NewGuid(), Position = 1, TrackId = "video.2.256", Codec = "H264", Type = "video" },
                    ],
                },
            };
            evt.Visualizations =
            [
                new RingLocationHistoryVisualization { Id = Guid.NewGuid(), Kind = "cloud", Schema = "CloudMediaVisualization", Media = [media] },
                new RingLocationHistoryVisualization { Id = Guid.NewGuid(), Kind = "local", Schema = "LocalMediaVisualization", Media = [] },
            ];
            return evt;
        }

        private static RingEventDetails BuildLocationHistoryDetails(Guid rowId)
        {
            return new()
            {
                Doorbot = new RingEventDoorbot { Id = Guid.NewGuid(), Source = RingEventSources.LocationHistory, SourceRowId = rowId, DoorbotId = 86401464, Description = "Sanitized description", Type = "doorbell" },
                Properties = new RingEventProperties { Id = Guid.NewGuid(), Source = RingEventSources.LocationHistory, SourceRowId = rowId, IsAlexa = false, IsSidewalk = false, IsAutoreply = false, StarkReviewed = false },
                CvProperties = new RingEventCvProperties
                {
                    Id = Guid.NewGuid(),
                    Source = RingEventSources.LocationHistory,
                    SourceRowId = rowId,
                    PersonDetected = true,
                    StreamBroken = false,
                    DetectionType = "human",
                    DetectionTypes = [new RingEventCvDetectionType { Id = Guid.NewGuid(), Position = 0, DetectionType = "human", VerifiedTimestamps = [new RingEventCvVerifiedTimestamp { Id = Guid.NewGuid(), Position = 0, TimestampMs = 1792178874177 }] }],
                    DetectionDetails = [new RingEventCvDetectionDetail { Id = Guid.NewGuid(), Position = 0, TimestampMs = 1792178874177, Type = "human" }],
                },
            };
        }

        private static RingAppLocationHistoryMessage BuildAppLocationHistory(string eventId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                Seq = 0,
                Msg = "DataUpdate",
                DataType = "DeviceInfoDocType",
                Src = "aps",
                EventId = eventId,
                EventPublishedTsMs = 1792177910381,
                RequestSeq = 0,
                AffectedEntityId = "43c0403c-19ad-42e8-b5bc-5694c8e8d9d7",
                DeviceFamily = "alarm",
                InitiatingEntityType = "user",
                AffectedEntityType = "device",
                ForwarderTopic = "location:11111111-2222-3333-4444-555555555555/aps",
                AccountId = "11111111-2222-3333-4444-555555555555",
                RequestSessionId = 0,
                EventLevel = 10,
                InitiatingEntityId = "00000000",
                EventOccurredTsMs = 1792177910381,
                AffectedEntityName = "Alarm",
                ProgramId = "a551631c-0e88-4097-8e1a-9595202c2ba9",
                Bodies =
            [
                new RingAppLocationHistoryBody
                {
                    Id = Guid.NewGuid(), Position = 0, DeviceType = "security-panel", CatalogId = "69cd7da3", Zid = "43c0403c", LastUpdate = 1792177910381,
                    AdapterType = "none", BatteryStatus = "none", SubCategoryId = 0, DeviceName = "Sanitized deviceName", CategoryId = 1, RoomName = "Sanitized roomName",
                    DeviceState = new RingAppLocationHistoryDeviceState
                    {
                        Id = Guid.NewGuid(), Mode = "some", SirenState = "off",
                        Modes =
                        [
                            new RingAppLocationHistoryDeviceMode { Id = Guid.NewGuid(), ModeName = "all", TransitionDelay = 120, Editable = true, Removable = false, ChirpsEnabled = false, EntryDelay = 30 },
                            new RingAppLocationHistoryDeviceMode { Id = Guid.NewGuid(), ModeName = "some", TransitionDelay = 0, Editable = true, Removable = false, ChirpsEnabled = false, EntryDelay = 30 },
                            new RingAppLocationHistoryDeviceMode { Id = Guid.NewGuid(), ModeName = "none", TransitionDelay = 0, Editable = false, Removable = false, ChirpsEnabled = false, EntryDelay = 30 },
                        ],
                    },
                    Impulses = [new RingAppLocationHistoryImpulse { Id = Guid.NewGuid(), Position = 0, ImpulseType = "command.complete", CommandType = "security-panel.switch-mode", DataMode = "some" }],
                },
            ],
            };
        }

        [Fact]
        public async Task RingEventHistoryWriter_AppLocationHistoryRoundTrip_PersistsBodyModesAndImpulses()
        {
            RingAppLocationHistoryMessage message = BuildAppLocationHistory("evt-app-1");
            await _writer.ReplaceAppLocationHistoryMessageAsync(message, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            RingAppLocationHistoryMessage stored = await db.RingAppLocationHistoryMessages.SingleAsync();
            Assert.Equal("aps", stored.Src);
            Assert.Equal(10, stored.EventLevel);
            RingAppLocationHistoryBody body = await db.RingAppLocationHistoryBodies.SingleAsync(b => b.RingAppLocationHistoryMessageId == message.Id);
            Assert.Equal("Sanitized deviceName", body.DeviceName);
            RingAppLocationHistoryDeviceState state = await db.RingAppLocationHistoryDeviceStates.SingleAsync(s => s.RingAppLocationHistoryBodyId == body.Id);
            Assert.Equal("off", state.SirenState);
            Assert.Equal(3, await db.RingAppLocationHistoryDeviceModes.CountAsync(m => m.RingAppLocationHistoryDeviceStateId == state.Id));
            Assert.Equal(120, (await db.RingAppLocationHistoryDeviceModes.SingleAsync(m => m.ModeName == "all")).TransitionDelay);
            Assert.Equal("security-panel.switch-mode", (await db.RingAppLocationHistoryImpulses.SingleAsync(i => i.RingAppLocationHistoryBodyId == body.Id)).CommandType);
        }

        [Fact]
        public async Task RingEventHistoryWriter_AppLocationHistoryReplaceSameEvent_RemovesPreviousBodies()
        {
            await _writer.ReplaceAppLocationHistoryMessageAsync(BuildAppLocationHistory("evt-app-2"), CancellationToken.None);
            await _writer.ReplaceAppLocationHistoryMessageAsync(BuildAppLocationHistory("evt-app-2"), CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(1, await db.RingAppLocationHistoryMessages.CountAsync());
            Assert.Equal(1, await db.RingAppLocationHistoryBodies.CountAsync());
            Assert.Equal(3, await db.RingAppLocationHistoryDeviceModes.CountAsync());
            Assert.Equal(1, await db.RingAppLocationHistoryImpulses.CountAsync());
        }

        [Fact]
        public async Task RingEventHistoryWriter_DoorbotHistoryRoundTrip_PersistsRecordingSirenAndPlacement()
        {
            var evt = new RingDoorbotHistoryEvent
            {
                Id = Guid.NewGuid(),
                ProviderEventId = "12345",
                CreatedAt = "2026-10-16T19:27:46Z",
                Answered = false,
                Kind = "motion",
                Favorite = false,
                SnapshotUrl = "https://sanitized.example.invalid/snapshot",
                Duration = 41,
                IsE2ee = false,
                HadSubscription = true,
                OwnerId = "00000000",
                Riid = "riid-4",
            };
            var details = new RingEventDetails
            {
                Recording = new RingEventRecording { Id = Guid.NewGuid(), Source = RingEventSources.DoorbotHistory, SourceRowId = evt.Id, Status = "ready" },
                Siren = new RingEventSiren { Id = Guid.NewGuid(), Source = RingEventSources.DoorbotHistory, SourceRowId = evt.Id, Armed = false, AlarmState = "off" },
                DevicePlacement = new RingEventDevicePlacement { Id = Guid.NewGuid(), Source = RingEventSources.DoorbotHistory, SourceRowId = evt.Id, Room = "Sanitized room", Area = "Sanitized area", ZoneId = "zone-1" },
                Geolocation = new RingEventGeolocation { Id = Guid.NewGuid(), Source = RingEventSources.DoorbotHistory, SourceRowId = evt.Id, Latitude = 1, Longitude = 2, Accuracy = 3, TimestampMs = 4, LocationSource = "gps" },
            };

            await _writer.ReplaceDoorbotHistoryEventAsync(evt, details, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            RingDoorbotHistoryEvent stored = await db.RingDoorbotHistoryEvents.SingleAsync();
            Assert.Equal(41, stored.Duration);
            Assert.Equal("ready", (await db.RingEventRecordings.SingleAsync(r => r.SourceRowId == evt.Id)).Status);
            Assert.Equal("zone-1", (await db.RingEventDevicePlacements.SingleAsync(p => p.SourceRowId == evt.Id)).ZoneId);
            Assert.Equal("off", (await db.RingEventSirens.SingleAsync(s => s.SourceRowId == evt.Id)).AlarmState);
            Assert.Equal("gps", (await db.RingEventGeolocations.SingleAsync(g => g.SourceRowId == evt.Id)).LocationSource);
        }
    }
}
