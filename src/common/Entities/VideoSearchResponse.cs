using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/video_search/history - a date-range
    /// search over recorded events that, unlike GetDoorbotsHistory(), returns direct (temporary,
    /// pre-signed) download URLs inline rather than requiring the separate dings/{id}/share/download
    /// polling GetDoorbotHistoryRecording() does. Confirmed against a live account via ApiTester.
    /// </summary>
    public class VideoSearchResponse
    {
        [JsonPropertyName("video_search")]
        public List<VideoSearchItem> VideoSearch { get; set; }
    }

    /// <summary>
    /// A single recorded event returned by video_search/history. Field shape confirmed against a
    /// live account via ApiTester - notably created_at/updated_at are epoch milliseconds here,
    /// unlike the ISO-8601 string GetDoorbotsHistory() returns for the same concept.
    /// </summary>
    public class VideoSearchItem
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("ding_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DingId { get; set; }

        [JsonPropertyName("created_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? CreatedAtUnixMs { get; set; }

        /// <summary>UTC date/time this event was recorded, converted from <see cref="CreatedAtUnixMs"/>.</summary>
        public DateTime? CreatedAt => CreatedAtUnixMs.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtUnixMs.Value).UtcDateTime
            : null;

        [JsonPropertyName("updated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? UpdatedAtUnixMs { get; set; }

        [JsonPropertyName("kind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Kind { get; set; }

        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string State { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Duration { get; set; }

        [JsonPropertyName("favorite")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Favorite { get; set; }

        /// <summary>Temporary, pre-signed high-quality download URL - expires; download promptly.</summary>
        [JsonPropertyName("hq_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string HqUrl { get; set; }

        /// <summary>Temporary, pre-signed low-quality download URL - expires; download promptly.</summary>
        [JsonPropertyName("lq_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LqUrl { get; set; }

        [JsonPropertyName("thumbnail_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ThumbnailUrl { get; set; }

        [JsonPropertyName("untranscoded_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UntranscodedUrl { get; set; }

        [JsonPropertyName("doorbot")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Doorbot Doorbot { get; set; }

        [JsonPropertyName("cv_properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CvProperties CvProperties { get; set; }

        /// <summary>
        /// Event owner/account ID.
        /// </summary>
        [JsonPropertyName("owner_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string OwnerId { get; set; }

        /// <summary>
        /// Event source ID.
        /// </summary>
        [JsonPropertyName("source_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SourceId { get; set; }

        /// <summary>
        /// Whether this event/recording is end-to-end encrypted.
        /// </summary>
        [JsonPropertyName("is_e2ee")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsE2ee { get; set; }

        /// <summary>
        /// Device battery percentage at the time of this event.
        /// Ring returns this as a string (e.g. "90") that requires conversion.
        /// Null if device is wired or battery info not available.
        /// </summary>
        [JsonPropertyName("battery_percentage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BatteryPercentage { get; set; }

        /// <summary>
        /// Device battery status category (e.g. "full", "good", "low", "critical").
        /// Null if not available.
        /// </summary>
        [JsonPropertyName("battery_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BatteryStatus { get; set; }

        /// <summary>
        /// Device placement/location information for this event.
        /// Null if placement data not available.
        /// </summary>
        [JsonPropertyName("device_placement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VideoSearchDevicePlacement DevicePlacement { get; set; }

        /// <summary>
        /// Geolocation data for the event (if available from mobile device or location services).
        /// Null if geolocation not available.
        /// </summary>
        [JsonPropertyName("geolocation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VideoSearchGeolocation Geolocation { get; set; }

        /// <summary>
        /// Subscription receipt/billing info associated with this event capture.
        /// Null if not applicable or not available.
        /// </summary>
        [JsonPropertyName("subscription_receipt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VideoSearchSubscriptionReceipt SubscriptionReceipt { get; set; }

        /// <summary>
        /// Manifest ID for streaming/playback.
        /// </summary>
        [JsonPropertyName("manifest_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ManifestId { get; set; }

        /// <summary>
        /// Duration (in seconds) of preroll content before the main recording.
        /// </summary>
        [JsonPropertyName("preroll_duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? PrerollDuration { get; set; }

        /// <summary>
        /// Whether the account had an active subscription when this event occurred.
        /// </summary>
        [JsonPropertyName("had_subscription")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? HadSubscription { get; set; }

        /// <summary>
        /// URL to radar/motion heatmap data for this event.
        /// Null if not available.
        /// </summary>
        [JsonPropertyName("radar_data_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RadarDataUrl { get; set; }

        /// <summary>
        /// Extended properties/metadata for this video event.
        /// </summary>
        [JsonPropertyName("properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VideoSearchProperties Properties { get; set; }

        /// <summary>
        /// Ring request/interaction ID for tracking purposes.
        /// </summary>
        [JsonPropertyName("riid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Riid { get; set; }
    }

    /// <summary>
    /// Extended properties for a video search result.
    /// </summary>
    public class VideoSearchProperties
    {
        [JsonPropertyName("is_alexa")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsAlexa { get; set; }

        [JsonPropertyName("is_sidewalk")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsSidewalk { get; set; }

        [JsonPropertyName("is_autoreply")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsAutoreply { get; set; }

        [JsonPropertyName("package_pickup_detected")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? PackagePickupDetected { get; set; }

        [JsonPropertyName("detection_confidence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DetectionConfidence { get; set; }

        [JsonPropertyName("stark_reviewed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? StarkReviewed { get; set; }
    }

    /// <summary>
    /// Device placement/location metadata for a video search result.
    /// </summary>
    public class VideoSearchDevicePlacement
    {
        [JsonPropertyName("room")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Room { get; set; }

        [JsonPropertyName("area")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Area { get; set; }

        [JsonPropertyName("zone_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ZoneId { get; set; }
    }

    /// <summary>
    /// Geolocation data from mobile device or location services.
    /// </summary>
    public class VideoSearchGeolocation
    {
        [JsonPropertyName("latitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Longitude { get; set; }

        [JsonPropertyName("accuracy")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Accuracy { get; set; }

        [JsonPropertyName("timestamp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Timestamp { get; set; }

        [JsonPropertyName("source")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Source { get; set; }
    }

    /// <summary>
    /// Subscription receipt info for event capture.
    /// </summary>
    public class VideoSearchSubscriptionReceipt
    {
        [JsonPropertyName("subscription_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SubscriptionId { get; set; }

        [JsonPropertyName("subscription_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SubscriptionType { get; set; }

        [JsonPropertyName("captured_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? CapturedAt { get; set; }

        [JsonPropertyName("expires_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? ExpiresAt { get; set; }
    }
}
