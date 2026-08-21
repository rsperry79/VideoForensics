using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ring.Api.Video.Metadata.Models
{
    /// <summary>
    /// Extracted video metadata from Ring event data, ready to be written to video files.
    /// Follows conventions compatible with PhotoPrism for event categorization and person detection.
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>
        /// Date and time when the event occurred (used for creation date).
        /// </summary>
        [JsonPropertyName("event_datetime")]
        public DateTime? EventDateTime { get; set; }

        /// <summary>
        /// Device GPS latitude coordinate.
        /// </summary>
        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        /// <summary>
        /// Device GPS longitude coordinate.
        /// </summary>
        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        /// <summary>
        /// Device street address. Used as fallback if GPS coordinates unavailable.
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        /// <summary>
        /// Received signal strength indicator (RSSI) at time of event.
        /// </summary>
        [JsonPropertyName("rssi")]
        public double? Rssi { get; set; }

        /// <summary>
        /// Battery percentage at time of event (0-100).
        /// </summary>
        [JsonPropertyName("battery_percentage")]
        public int? BatteryPercentage { get; set; }

        /// <summary>
        /// Whether motion was detected in the recording.
        /// </summary>
        [JsonPropertyName("motion_detected")]
        public bool? MotionDetected { get; set; }

        /// <summary>
        /// Whether a person was detected by Ring's computer vision system.
        /// </summary>
        [JsonPropertyName("person_detected")]
        public bool? PersonDetected { get; set; }

        /// <summary>
        /// Detection type classification (e.g., "human", "vehicle", "animal", "other_motion").
        /// </summary>
        [JsonPropertyName("detection_type")]
        public string? DetectionType { get; set; }

        /// <summary>
        /// Confidence score for person detection (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("detection_confidence")]
        public double? DetectionConfidence { get; set; }

        /// <summary>
        /// Device name/description for identification.
        /// </summary>
        [JsonPropertyName("device_name")]
        public string? DeviceName { get; set; }

        /// <summary>
        /// Device manufacturer (e.g., "Amazon", "Ring").
        /// </summary>
        [JsonPropertyName("device_manufacturer")]
        public string? DeviceManufacturer { get; set; }

        /// <summary>
        /// Device model (e.g., "Doorbell 2", "Stick Up Cam Pro").
        /// </summary>
        [JsonPropertyName("device_model")]
        public string? DeviceModel { get; set; }

        /// <summary>
        /// Device timezone information.
        /// </summary>
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        /// <summary>
        /// Event type for PhotoPrism categorization (e.g., "motion", "person", "ring").
        /// </summary>
        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        /// <summary>
        /// Any additional keywords/tags for organizing in PhotoPrism.
        /// </summary>
        [JsonPropertyName("keywords")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Keywords { get; set; }

        /// <summary>
        /// Comment/description for the metadata.
        /// </summary>
        [JsonPropertyName("comment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Comment { get; set; }

        /// <summary>
        /// Ring event kind (e.g., "motion", "doorbell").
        /// </summary>
        [JsonPropertyName("ring_event_kind")]
        public string? RingEventKind { get; set; }

        /// <summary>
        /// Raw Ring event ID for reference.
        /// </summary>
        [JsonPropertyName("ring_event_id")]
        public long? RingEventId { get; set; }

        // ============================================================================
        // Evidence Integrity & Anomaly Detection Fields (DV Support)
        // ============================================================================

        /// <summary>
        /// Whether Ring reported the video stream was broken/incomplete.
        /// CRITICAL: indicates jammed video, connection loss, or tampering.
        /// </summary>
        [JsonPropertyName("stream_broken")]
        public bool? StreamBroken { get; set; }

        /// <summary>
        /// Anomaly score for unusual activity (0.0 to 1.0).
        /// Higher values indicate suspicious/abnormal behavior.
        /// </summary>
        [JsonPropertyName("anomaly_score")]
        public double? AnomalyScore { get; set; }

        /// <summary>
        /// Security alerts detected by Ring AI (e.g., "loud noise", "glass breaking").
        /// Important for capturing environmental evidence of violence.
        /// </summary>
        [JsonPropertyName("security_alerts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? SecurityAlerts { get; set; }

        /// <summary>
        /// Severity level of security alerts if present.
        /// </summary>
        [JsonPropertyName("alert_severity")]
        public string? AlertSeverity { get; set; }

        /// <summary>
        /// Full AI-generated description of the event.
        /// </summary>
        [JsonPropertyName("full_description")]
        public string? FullDescription { get; set; }

        /// <summary>
        /// Short AI-generated summary of the event.
        /// </summary>
        [JsonPropertyName("short_description")]
        public string? ShortDescription { get; set; }

        /// <summary>
        /// Recognized people profiles detected in the video.
        /// CRITICAL: for identifying perpetrators and witnesses.
        /// </summary>
        [JsonPropertyName("recognized_profiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DetectedProfile>? RecognizedProfiles { get; set; }

        /// <summary>
        /// Motion detection zones where activity occurred.
        /// Helps identify specific areas of concern in the scene.
        /// </summary>
        [JsonPropertyName("detection_zones")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MotionZone>? DetectionZones { get; set; }

        /// <summary>
        /// Ring device firmware version at time of event.
        /// Important for evidence chain of custody and device consistency.
        /// </summary>
        [JsonPropertyName("device_firmware_version")]
        public string? DeviceFirmwareVersion { get; set; }

        /// <summary>
        /// Whether device owner had notifications/motion alerts enabled.
        /// Indicates if owner was expected to be aware of activity (intent marker).
        /// </summary>
        [JsonPropertyName("owner_notifications_enabled")]
        public bool? OwnerNotificationsEnabled { get; set; }

        /// <summary>
        /// Whether device had external/network connection at event time.
        /// False = may explain recording issues or inability to upload.
        /// </summary>
        [JsonPropertyName("device_online")]
        public bool? DeviceOnline { get; set; }

        /// <summary>
        /// Recording status from Ring (e.g., "ready", "failed").
        /// Indicates if recording completed successfully.
        /// </summary>
        [JsonPropertyName("recording_status")]
        public string? RecordingStatus { get; set; }

        /// <summary>
        /// File size of the recorded video in bytes.
        /// Unusually small size may indicate corruption, tampering, or failed recording.
        /// </summary>
        [JsonPropertyName("video_file_size")]
        public long? VideoFileSize { get; set; }

        /// <summary>
        /// User-applied or AI-suggested tags for this event.
        /// May include abuse categorizations or threat level indicators.
        /// </summary>
        [JsonPropertyName("event_tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? EventTags { get; set; }

        /// <summary>
        /// AI confidence in the detection model results (0.0 to 1.0).
        /// Lower confidence may indicate unclear evidence.
        /// </summary>
        [JsonPropertyName("model_confidence")]
        public double? ModelConfidence { get; set; }

        /// <summary>
        /// Ring AI model version used for analysis.
        /// Important for reproducibility and understanding detection behavior.
        /// </summary>
        [JsonPropertyName("ai_model_version")]
        public string? AiModelVersion { get; set; }

        /// <summary>
        /// Precise timestamps (epoch milliseconds) when AI confirmed detections.
        /// CRITICAL: establishes exact timeline of detected activity for frame extraction.
        /// </summary>
        [JsonPropertyName("verified_detection_timestamps")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<long>? VerifiedDetectionTimestamps { get; set; }

        /// <summary>
        /// Whether evidence needs review due to signal/connectivity issues.
        /// CRITICAL: indicates potential tampering, jamming, or device interference.
        /// </summary>
        [JsonPropertyName("needs_review")]
        public bool NeedsReview { get; set; }

        /// <summary>
        /// Reason why evidence needs review (low signal, packet loss, etc.).
        /// </summary>
        [JsonPropertyName("needs_review_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NeedsReviewReason { get; set; }

        /// <summary>
        /// Signal strength (RSSI) in dBm at time of event.
        /// Lower (more negative) values indicate weaker signal.
        /// -70 dBm or lower may indicate interference or tampering.
        /// </summary>
        [JsonPropertyName("rssi_dbm")]
        public int? RssiDbm { get; set; }

        /// <summary>
        /// Packet loss percentage at time of event.
        /// Higher values indicate network instability or intentional interference.
        /// </summary>
        [JsonPropertyName("packet_loss_percent")]
        public double? PacketLossPercent { get; set; }
    }

    /// <summary>
    /// A recognized person profile detected in a video.
    /// Important for identifying perpetrators and witnesses in DV cases.
    /// </summary>
    public class DetectedProfile
    {
        /// <summary>
        /// Profile ID in Ring system.
        /// </summary>
        [JsonPropertyName("profile_id")]
        public string? Id { get; set; }

        /// <summary>
        /// Name of the recognized person.
        /// </summary>
        [JsonPropertyName("profile_name")]
        public string? Name { get; set; }

        /// <summary>
        /// Confidence score for this identification (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("profile_confidence")]
        public double? Confidence { get; set; }

        /// <summary>
        /// URL to thumbnail image of the detected profile.
        /// </summary>
        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// A motion detection zone within the video frame.
    /// </summary>
    public class MotionZone
    {
        /// <summary>
        /// Zone ID identifier.
        /// </summary>
        [JsonPropertyName("zone_id")]
        public string? Id { get; set; }

        /// <summary>
        /// Human-readable name for the zone (e.g., "front door", "living room").
        /// </summary>
        [JsonPropertyName("zone_name")]
        public string? Name { get; set; }

        /// <summary>
        /// Confidence score for motion in this zone (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("zone_confidence")]
        public double? Confidence { get; set; }
    }
}
