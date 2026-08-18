using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/locations/{locationId}/events and
    /// .../locations/{locationId}/devices/{deviceId}/events, confirmed via a live ApiTester run.
    /// This is NOT the same shape as doorbots/history's DoorbotHistoryEvent - the two were
    /// originally, incorrectly, treated as interchangeable, which meant GetLocationEvents()/
    /// GetDeviceEvents() silently discarded nearly the entire payload (event_id/ding_id/owner_id
    /// etc. have no DoorbotHistoryEvent equivalent, so they just never got captured - no
    /// exception, since System.Text.Json ignores unmatched properties by default).
    /// </summary>
    public class LocationEventsResponse
    {
        [JsonPropertyName("events")]
        public List<LocationEvent> Events { get; set; }

        [JsonPropertyName("meta")]
        public LocationEventsMeta Meta { get; set; }
    }

    public class LocationEventsMeta
    {
        [JsonPropertyName("pagination_key")]
        public string PaginationKey { get; set; }
    }

    /// <summary>
    /// A single event from the locations/{id}/events (or .../devices/{id}/events) feed.
    /// </summary>
    public class LocationEvent
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("source_id")]
        public string SourceId { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }

        [JsonPropertyName("recorded")]
        public bool Recorded { get; set; }

        [JsonPropertyName("recording_status")]
        public string RecordingStatus { get; set; }

        [JsonPropertyName("is_e2ee")]
        public bool IsE2ee { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("had_subscription")]
        public bool HadSubscription { get; set; }

        [JsonPropertyName("owner_id")]
        public string OwnerId { get; set; }

        [JsonPropertyName("riid")]
        public string Riid { get; set; }

        [JsonPropertyName("doorbot_id")]
        public long? DoorbotId { get; set; }

        [JsonPropertyName("ding_id")]
        public long? DingId { get; set; }

        [JsonPropertyName("ding_id_str")]
        public string DingIdString { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        /// <summary>
        /// Minimal device summary - not the full Doorbot entity GetRingDevices() returns. Confirmed
        /// via a live capture to only carry id/description/type.
        /// </summary>
        [JsonPropertyName("doorbot")]
        public LocationEventDoorbot Doorbot { get; set; }

        [JsonPropertyName("cv_properties")]
        public CvProperties CvProperties { get; set; }

        [JsonPropertyName("properties")]
        public LocationEventProperties Properties { get; set; }
    }

    public class LocationEventDoorbot
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class LocationEventProperties
    {
        [JsonPropertyName("is_alexa")]
        public bool IsAlexa { get; set; }

        [JsonPropertyName("is_sidewalk")]
        public bool IsSidewalk { get; set; }

        [JsonPropertyName("is_autoreply")]
        public bool IsAutoreply { get; set; }

        [JsonPropertyName("stark_reviewed")]
        public bool StarkReviewed { get; set; }
    }
}
