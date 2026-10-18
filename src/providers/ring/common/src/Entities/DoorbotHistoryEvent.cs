using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class DoorbotHistoryEvent
    {
        /// <summary>
        /// Unique identifier of this historical event
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        /// <summary>
        /// Raw date time string when this event occurred
        /// </summary>
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        public DateTime? CreatedAtDateTime => field.HasValue ? field.Value : !DateTime.TryParse(CreatedAt, out DateTime result) ? null : (field = result);

        /// <summary>
        /// Boolean indicating if the ring was answered
        /// </summary>
        [JsonPropertyName("answered")]
        public bool Answered { get; set; }

        [JsonPropertyName("events")]
        public List<object> Events { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }

        [JsonPropertyName("snapshot_url")]
        public string SnapshotUrl { get; set; }

        [JsonPropertyName("recording")]
        public DoorbotHistoryEventRecording Recording { get; set; }

        /// <summary>
        /// The doorbot/camera this event belongs to. The /doorbots/history endpoint returns only a
        /// minimal stub here — Id and Description are populated, but DeviceId, BatteryLife,
        /// FirmwareVersion, and Health all come back empty/null. Match events to devices via
        /// Doorbot.Id (the numeric Ring id), not DeviceId (the hex device_id used by /devices) — the
        /// two are different identifiers and DeviceId is never present on this embedded object.
        /// </summary>
        [JsonPropertyName("doorbot")]
        public Doorbot Doorbot { get; set; }

        /// <summary>
        /// Computer vision properties (person detection, detection type, etc.) for this event, when available.
        /// </summary>
        [JsonPropertyName("cv_properties")]
        public CvProperties CvProperties { get; set; }

        /// <summary>Length of the event in seconds. Ring sends decimals (40.0) on some events, so it is rounded on read.</summary>
        [JsonPropertyName("duration")]
        [JsonConverter(typeof(VideoForensics.Providers.Ring.Converters.DecimalToIntConverter))]
        public int? Duration { get; set; }

        [JsonPropertyName("is_e2ee")]
        public bool? IsE2ee { get; set; }

        [JsonPropertyName("had_subscription")]
        public bool? HadSubscription { get; set; }

        /// <summary>Event owner/account id.</summary>
        [JsonPropertyName("owner_id")]
        public string OwnerId { get; set; }

        [JsonPropertyName("riid")]
        public string Riid { get; set; }

        [JsonPropertyName("properties")]
        public LocationEventProperties Properties { get; set; }

        [JsonPropertyName("device_placement")]
        public LocationEventDevicePlacement DevicePlacement { get; set; }

        [JsonPropertyName("geolocation")]
        public LocationEventGeolocation Geolocation { get; set; }

        [JsonPropertyName("last_location")]
        public LocationEventLocation LastLocation { get; set; }

        [JsonPropertyName("siren")]
        public LocationEventSirenInfo Siren { get; set; }
    }
}
