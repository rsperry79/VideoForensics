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
    }
}
