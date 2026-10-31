using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Maps the sanitized captured video search, location events, location history, doorbot history and app location
    /// history payloads onto persistence entities, and checks that the mapped values match the capture.
    /// </summary>
    public class RingEventHistoryMapperTests
    {
        private static readonly Guid AccountId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        private const string LocationId = "11111111-2222-3333-4444-555555555555";

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
            return File.ReadAllText(path);
        }

        [Fact]
        public void RingEventHistoryMapper_MapVideoSearch_CapturedItem_MapsEveryField()
        {
            VideoSearchResponse response = JsonSerializer.Deserialize<VideoSearchResponse>(ReadFixture("video-search-sanitized.json"))!;
            VideoSearchItem item = response.VideoSearch[0];

            MappedRingEvent<RingVideoSearchEvent> mapped = RingEventHistoryMapper.MapVideoSearch(item, AccountId);

            RingVideoSearchEvent evt = mapped.Event;
            Assert.Equal(AccountId, evt.RingAccountId);
            Assert.Equal(item.DingId, evt.ProviderEventId);
            Assert.Null(evt.RingItemId);
            Assert.Equal(1792178873638, evt.CreatedAtMs);
            Assert.Equal(1792178914777000, evt.UpdatedAtMs);
            Assert.Equal("motion", evt.Kind);
            Assert.Equal("timed_out", evt.State);
            Assert.Equal(41, evt.DurationSeconds);
            Assert.False(evt.Favorite);
            Assert.Equal(item.HqUrl, evt.HqUrl);
            Assert.Equal(item.LqUrl, evt.LqUrl);
            Assert.Equal(item.ThumbnailUrl, evt.ThumbnailUrl);
            Assert.Equal(item.UntranscodedUrl, evt.UntranscodedUrl);
            Assert.False(evt.IsE2ee);
            Assert.Null(evt.ManifestId);
            Assert.Equal(7400, evt.PrerollDuration);
            Assert.True(evt.HadSubscription);
            Assert.Null(evt.RadarDataUrl);
            Assert.Equal(item.OwnerId, evt.OwnerId);
            Assert.Equal(item.SourceId, evt.SourceId);
            Assert.Equal(item.Riid, evt.Riid);
            Assert.Null(evt.BatteryPercentage);
            Assert.Null(evt.BatteryStatus);

            Assert.Equal(86401464, mapped.Details.Doorbot!.DoorbotId);
            Assert.Equal("Sanitized description", mapped.Details.Doorbot.Description);
            Assert.Equal(item.Doorbot.Type, mapped.Details.Doorbot.Type);
            Assert.Null(mapped.Details.Properties!.PackagePickupDetected);
            Assert.False(mapped.Details.Properties.IsAlexa);
            Assert.Null(mapped.Details.DevicePlacement);
            Assert.Null(mapped.Details.Geolocation);
            Assert.Null(mapped.Details.SubscriptionReceipt);
            Assert.Equal("human", mapped.Details.CvProperties!.DetectionType);
            Assert.True(mapped.Details.CvProperties.PersonDetected);
            Assert.False(mapped.Details.CvProperties.StreamBroken);
            Assert.Equal("human", mapped.Details.CvProperties.DetectionTypes.Single().DetectionType);
            Assert.Equal(1792178874177, mapped.Details.CvProperties.DetectionTypes.Single().VerifiedTimestamps.Single().TimestampMs);
            Assert.Empty(mapped.Details.CvProperties.Tags);
        }

        [Fact]
        public void RingEventHistoryMapper_MapLocationEvent_CapturedEvent_MapsEveryField()
        {
            LocationEventsResponse response = JsonSerializer.Deserialize<LocationEventsResponse>(ReadFixture("location-events-sanitized.json"))!;
            LocationEvent source = response.Events[0];

            MappedRingEvent<RingLocationEvent> mapped = RingEventHistoryMapper.MapLocationEvent(source, LocationId);

            RingLocationEvent evt = mapped.Event;
            Assert.Equal(LocationId, evt.ProviderLocationId);
            Assert.Equal(source.EventId, evt.ProviderEventId);
            Assert.Equal(source.SourceId, evt.SourceId);
            Assert.Equal("motion", evt.EventType);
            Assert.Equal("timed_out", evt.State);
            Assert.False(evt.Favorite);
            Assert.True(evt.Recorded);
            Assert.Equal("ready", evt.RecordingStatus);
            Assert.False(evt.IsE2ee);
            Assert.Equal("2026-10-16T19:27:46Z", evt.CreatedAt);
            Assert.True(evt.HadSubscription);
            Assert.Equal(source.OwnerId, evt.OwnerId);
            Assert.Equal(source.Riid, evt.Riid);
            Assert.Equal(86401464, evt.DoorbotId);
            Assert.Equal(source.DingId, evt.DingId);
            Assert.Equal(source.DingIdString, evt.DingIdString);
            Assert.Equal("motion", evt.Kind);
            Assert.Null(evt.BatteryPercentage);

            Assert.Equal(86401464, mapped.Details.Doorbot!.DoorbotId);
            Assert.Equal("doorbell_graham_cracker", mapped.Details.Doorbot.Type);
            Assert.False(mapped.Details.Properties!.IsSidewalk);
            Assert.Null(mapped.Details.Siren);
            Assert.Null(mapped.Details.LastLocation);
            Assert.Equal("human", mapped.Details.CvProperties!.DetectionType);
        }

        [Fact]
        public void RingEventHistoryMapper_MapLocationEventsPage_CapturedMeta_MapsPaginationKey()
        {
            LocationEventsResponse response = JsonSerializer.Deserialize<LocationEventsResponse>(ReadFixture("location-events-sanitized.json"))!;

            RingLocationEventPage page = RingEventHistoryMapper.MapLocationEventsPage(LocationId, response.Meta);

            Assert.Equal(LocationId, page.ProviderLocationId);
            Assert.Equal(response.Meta.PaginationKey, page.PaginationKey);
        }

        [Fact]
        public void RingEventHistoryMapper_MapLocationHistoryItem_CapturedItem_MapsMediaMetadataAndTracks()
        {
            LocationHistoryResponse response = JsonSerializer.Deserialize<LocationHistoryResponse>(ReadFixture("location-history-sanitized.json"))!;
            LocationHistoryItem source = response.Items[0];

            MappedRingEvent<RingLocationHistoryEvent> mapped = RingEventHistoryMapper.MapLocationHistoryItem(source, LocationId);

            RingLocationHistoryEvent evt = mapped.Event;
            Assert.Equal(source.EventId, evt.ProviderEventId);
            Assert.Equal("Event", evt.Schema);
            Assert.Equal("2026-10-16T19:27:46.238Z", evt.StartTime);
            Assert.Equal("motion", evt.EventType);
            Assert.Equal(48231, evt.DurationMs);
            Assert.Equal(41000, evt.SessionDurationMs);
            Assert.Equal("ready", evt.RecordingStatus);
            Assert.False(evt.IsFavorite);
            Assert.False(evt.IsWatched);
            Assert.False(evt.IsActive);
            Assert.False(evt.UserActionDisabled);
            Assert.Null(evt.Origin);
            Assert.Equal(source.OwnerId, evt.OwnerId);

            Assert.Equal(2, evt.Visualizations.Count);
            RingLocationHistoryVisualization cloud = evt.Visualizations.Single(v => v.Kind == "cloud");
            Assert.Equal("CloudMediaVisualization", cloud.Schema);
            Assert.Equal(2, cloud.Media.Count);
            RingLocationHistoryMedia media = cloud.Media[0];
            Assert.Equal(7400, media.PrerollDurationMs);
            Assert.Equal(48231, media.PlaybackDuration);
            Assert.Equal("VIDEO", media.FileType);
            Assert.Equal("Apsara", media.Source);
            Assert.Equal("H264", media.Metadata!.VideoEncoding);
            Assert.True(media.Metadata.TbaTranscodingEnabled);
            Assert.Equal(2, media.Metadata.Tracks.Count);
            Assert.Equal("video.2.256", media.Metadata.Tracks[1].TrackId);
            Assert.Equal("video", media.Metadata.Tracks[1].Type);
            Assert.Empty(evt.Visualizations.Single(v => v.Kind == "local").Media);

            Assert.Equal(86401464, mapped.Details.Doorbot!.DoorbotId);
            Assert.Equal("human", mapped.Details.CvProperties!.DetectionType);
            Assert.Equal(1792178874177, mapped.Details.CvProperties.DetectionDetails.Single().TimestampMs);
            Assert.Equal("human", mapped.Details.CvProperties.DetectionDetails.Single().Type);
        }

        [Fact]
        public void RingEventHistoryMapper_MapLocationHistoryPage_CapturedResponse_MapsPaginationKey()
        {
            LocationHistoryResponse response = JsonSerializer.Deserialize<LocationHistoryResponse>(ReadFixture("location-history-sanitized.json"))!;

            RingLocationHistoryPage page = RingEventHistoryMapper.MapLocationHistoryPage(LocationId, response.PaginationKey);

            Assert.Equal(response.PaginationKey, page.PaginationKey);
        }

        [Fact]
        public void RingEventHistoryMapper_MapDoorbotHistoryEvent_CapturedEvent_MapsEveryField()
        {
            List<DoorbotHistoryEvent> events = JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(ReadFixture("doorbot-history-sanitized.json"))!;
            DoorbotHistoryEvent source = events[0];

            MappedRingEvent<RingDoorbotHistoryEvent> mapped = RingEventHistoryMapper.MapDoorbotHistoryEvent(source);

            RingDoorbotHistoryEvent evt = mapped.Event;
            Assert.Equal(source.Id!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), evt.ProviderEventId);
            Assert.Equal(source.CreatedAt, evt.CreatedAt);
            Assert.False(evt.Answered);
            Assert.Equal("motion", evt.Kind);
            Assert.False(evt.Favorite);
            Assert.Equal(string.Empty, evt.SnapshotUrl);
            Assert.Equal(41, evt.Duration);
            Assert.False(evt.IsE2ee);
            Assert.True(evt.HadSubscription);
            Assert.Equal(source.OwnerId, evt.OwnerId);
            Assert.Equal(source.Riid, evt.Riid);

            Assert.Equal("ready", mapped.Details.Recording!.Status);
            Assert.Equal(86401464, mapped.Details.Doorbot!.DoorbotId);
            Assert.False(mapped.Details.Properties!.IsAutoreply);
            Assert.Null(mapped.Details.Siren);
            Assert.Null(mapped.Details.LastLocation);
            Assert.Null(mapped.Details.DevicePlacement);
            Assert.Null(mapped.Details.Geolocation);
        }

        [Fact]
        public void RingEventHistoryMapper_MapAppLocationHistory_CapturedMessage_MapsContextBodiesModesAndImpulses()
        {
            List<AppLocationHistoryMessage> messages = JsonSerializer.Deserialize<List<AppLocationHistoryMessage>>(ReadFixture("app-location-history-sanitized.json"))!;
            AppLocationHistoryMessage source = messages[0];

            RingAppLocationHistoryMessage message = RingEventHistoryMapper.MapAppLocationHistory(source);

            Assert.Equal("DataUpdate", message.Msg);
            Assert.Equal("DeviceInfoDocType", message.DataType);
            Assert.Equal("aps", message.Src);
            Assert.Equal(source.Context.EventId, message.EventId);
            Assert.Equal(source.Context.EventPublishedTsMs, message.EventPublishedTsMs);
            Assert.Equal(source.Context.ForwarderTopic, message.ForwarderTopic);
            Assert.Equal(source.Context.AccountId, message.AccountId);
            Assert.Equal(10, message.EventLevel);
            Assert.Equal("Alarm", message.AffectedEntityName);
            Assert.Equal(source.Context.ProgramId, message.ProgramId);

            RingAppLocationHistoryBody body = message.Bodies.Single();
            Assert.Equal("security-panel", body.DeviceType);
            Assert.Equal(1792177910381, body.LastUpdate);
            Assert.Equal("none", body.AdapterType);
            Assert.Equal("none", body.BatteryStatus);
            Assert.Equal("Sanitized deviceName", body.DeviceName);
            Assert.Equal("Sanitized roomName", body.RoomName);

            RingAppLocationHistoryDeviceState state = body.DeviceState!;
            Assert.Equal("some", state.Mode);
            Assert.Equal("off", state.SirenState);
            Assert.Equal(3, state.Modes.Count);
            Assert.Equal(120, state.Modes.Single(m => m.ModeName == "all").TransitionDelay);
            Assert.False(state.Modes.Single(m => m.ModeName == "none").Editable);

            RingAppLocationHistoryImpulse impulse = body.Impulses.Single();
            Assert.Equal("command.complete", impulse.ImpulseType);
            Assert.Equal("security-panel.switch-mode", impulse.CommandType);
            Assert.Equal("some", impulse.DataMode);
        }

        [Fact]
        public void RingEventHistoryMapper_MapDoorbotHistoryEvent_NullRingId_ProducesNullProviderEventId()
        {
            var source = new DoorbotHistoryEvent { Id = null };

            MappedRingEvent<RingDoorbotHistoryEvent> mapped = RingEventHistoryMapper.MapDoorbotHistoryEvent(source);

            Assert.Null(mapped.Event.ProviderEventId);
        }
    }
}
