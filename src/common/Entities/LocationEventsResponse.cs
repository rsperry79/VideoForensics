using System;
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

        /// <summary>
        /// Device placement/location information for this event.
        /// Null if placement data not available.
        /// </summary>
        [JsonPropertyName("device_placement")]
        public LocationEventDevicePlacement DevicePlacement { get; set; }

        /// <summary>
        /// Geolocation data for the event (if available from mobile device or location services).
        /// Null if geolocation not available.
        /// </summary>
        [JsonPropertyName("geolocation")]
        public LocationEventGeolocation Geolocation { get; set; }

        /// <summary>
        /// Subscription receipt/billing info associated with this event capture.
        /// Null if not applicable or not available.
        /// </summary>
        [JsonPropertyName("subscription_receipt")]
        public LocationEventSubscriptionReceipt SubscriptionReceipt { get; set; }

        /// <summary>
        /// Device battery percentage at the time of this event.
        /// Ring returns this as a string (e.g. "90") that requires conversion.
        /// Null if device is wired or battery info not available.
        /// </summary>
        [JsonPropertyName("battery_percentage")]
        public string BatteryPercentage { get; set; }

        /// <summary>
        /// Device battery status category (e.g. "full", "good", "low", "critical").
        /// Null if not available.
        /// </summary>
        [JsonPropertyName("battery_status")]
        public string BatteryStatus { get; set; }

        /// <summary>
        /// Last known device location when this event occurred.
        /// Null if location tracking not available.
        /// </summary>
        [JsonPropertyName("last_location")]
        public LocationEventLocation LastLocation { get; set; }

        /// <summary>
        /// Alarm/siren information if this event triggered any alarm.
        /// Null if no alarm associated with this event.
        /// </summary>
        [JsonPropertyName("siren")]
        public LocationEventSirenInfo Siren { get; set; }
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

        [JsonPropertyName("package_pickup_detected")]
        public bool? PackagePickupDetected { get; set; }

        [JsonPropertyName("detection_confidence")]
        public double? DetectionConfidence { get; set; }
    }

    /// <summary>
    /// Device placement/location metadata for an event.
    /// </summary>
    public class LocationEventDevicePlacement
    {
        [JsonPropertyName("room")]
        public string Room { get; set; }

        [JsonPropertyName("area")]
        public string Area { get; set; }

        [JsonPropertyName("zone_id")]
        public string ZoneId { get; set; }
    }

    /// <summary>
    /// Geolocation data from mobile device or location services.
    /// </summary>
    public class LocationEventGeolocation
    {
        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("accuracy")]
        public double? Accuracy { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    /// <summary>
    /// Subscription receipt info for event capture.
    /// </summary>
    public class LocationEventSubscriptionReceipt
    {
        [JsonPropertyName("subscription_id")]
        public string SubscriptionId { get; set; }

        [JsonPropertyName("subscription_type")]
        public string SubscriptionType { get; set; }

        [JsonPropertyName("captured_at")]
        public long? CapturedAt { get; set; }

        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Last known location of a device when an event occurred.
    /// </summary>
    public class LocationEventLocation
    {
        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("accuracy")]
        public double? Accuracy { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// Alarm/siren information associated with an event.
    /// </summary>
    public class LocationEventSirenInfo
    {
        [JsonPropertyName("armed")]
        public bool? Armed { get; set; }

        [JsonPropertyName("triggered_at")]
        public long? TriggeredAt { get; set; }

        [JsonPropertyName("trigger_type")]
        public string TriggerType { get; set; }

        [JsonPropertyName("alarm_state")]
        public string AlarmState { get; set; }
    }
}
