using System;
using System.Collections.Generic;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Models
{
    /// <summary>
    /// Metadata extracted from a snapshot event, including image-specific fields.
    /// Contains both Ring event data and EXIF-compatible image properties.
    /// </summary>
    public class SnapshotMetadata
    {
        /// <summary>
        /// DateTime of the snapshot event.
        /// </summary>
        public DateTime? EventDateTime { get; set; }

        /// <summary>
        /// Latitude of the device location.
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Longitude of the device location.
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Street address of the device location.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Timezone of the device.
        /// </summary>
        public string? Timezone { get; set; }

        /// <summary>
        /// RSSI (signal strength) of the device's network connection.
        /// </summary>
        public int? Rssi { get; set; }

        /// <summary>
        /// Battery percentage of the device.
        /// </summary>
        public int? BatteryPercentage { get; set; }

        /// <summary>
        /// Whether motion was detected in the snapshot.
        /// </summary>
        public bool? MotionDetected { get; set; }

        /// <summary>
        /// Whether a person was detected in the snapshot.
        /// </summary>
        public bool? PersonDetected { get; set; }

        /// <summary>
        /// Type of detection (e.g., "motion", "person").
        /// </summary>
        public string? DetectionType { get; set; }

        /// <summary>
        /// Confidence level of the detection (0-100).
        /// </summary>
        public int? DetectionConfidence { get; set; }

        /// <summary>
        /// Name of the Ring device.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Manufacturer of the device (typically "Amazon").
        /// </summary>
        public string? DeviceManufacturer { get; set; }

        /// <summary>
        /// Model of the Ring device.
        /// </summary>
        public string? DeviceModel { get; set; }

        /// <summary>
        /// Type of event (e.g., "motion", "person").
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Keywords/tags for the snapshot for PhotoPrism compatibility.
        /// </summary>
        public List<string>? Keywords { get; set; }

        /// <summary>
        /// User comment or description for the snapshot.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Ring event kind identifier.
        /// </summary>
        public string? RingEventKind { get; set; }

        /// <summary>
        /// Ring event ID.
        /// </summary>
        public string? RingEventId { get; set; }

        // Image-Specific Fields

        /// <summary>
        /// Image format (JPEG, WebP, PNG).
        /// </summary>
        public string? ImageFormat { get; set; }

        /// <summary>
        /// Image dimensions (width × height).
        /// </summary>
        public string? ImageDimensions { get; set; }

        /// <summary>
        /// Image color space (sRGB, Adobe RGB, etc.).
        /// </summary>
        public string? ImageColorSpace { get; set; }

        /// <summary>
        /// Image file size in bytes.
        /// </summary>
        public long ImageFileSize { get; set; }

        /// <summary>
        /// Whether the image contains EXIF data.
        /// </summary>
        public bool HasExif { get; set; }

        /// <summary>
        /// EXIF orientation (1-8 for rotation values).
        /// </summary>
        public int ExifOrientation { get; set; } = 1;

        /// <summary>
        /// Estimated image quality score (1-100).
        /// </summary>
        public int ImageQualityScore { get; set; }

        // ============================================================================
        // Evidence Integrity & Anomaly Detection Fields (DV Support)
        // ============================================================================

        /// <summary>
        /// Whether Ring reported the image stream was broken/incomplete.
        /// CRITICAL: indicates jammed camera, connection loss, or tampering.
        /// </summary>
        public bool? StreamBroken { get; set; }

        /// <summary>
        /// Anomaly score for unusual activity (0.0 to 1.0).
        /// Higher values indicate suspicious/abnormal behavior.
        /// </summary>
        public double? AnomalyScore { get; set; }

        /// <summary>
        /// Security alerts detected by Ring AI (e.g., "loud noise", "glass breaking").
        /// Important for capturing environmental evidence of violence.
        /// </summary>
        public List<string>? SecurityAlerts { get; set; }

        /// <summary>
        /// Severity level of security alerts if present.
        /// </summary>
        public string? AlertSeverity { get; set; }

        /// <summary>
        /// Full AI-generated description of the event.
        /// </summary>
        public string? FullDescription { get; set; }

        /// <summary>
        /// Short AI-generated summary of the event.
        /// </summary>
        public string? ShortDescription { get; set; }

        /// <summary>
        /// Recognized people profiles detected in the snapshot.
        /// CRITICAL: for identifying perpetrators and witnesses.
        /// </summary>
        public List<DetectedProfile>? RecognizedProfiles { get; set; }

        /// <summary>
        /// Motion detection zones where activity occurred.
        /// Helps identify specific areas of concern in the scene.
        /// </summary>
        public List<MotionZone>? DetectionZones { get; set; }

        /// <summary>
        /// Precise timestamps (epoch milliseconds) when AI confirmed detections.
        /// CRITICAL: establishes exact timeline of detected activity.
        /// </summary>
        public List<long>? VerifiedDetectionTimestamps { get; set; }

        /// <summary>
        /// Ring device firmware version at time of event.
        /// Important for evidence chain of custody and device consistency.
        /// </summary>
        public string? DeviceFirmwareVersion { get; set; }

        /// <summary>
        /// Whether device owner had notifications/motion alerts enabled.
        /// Indicates if owner was expected to be aware of activity (intent marker).
        /// </summary>
        public bool? OwnerNotificationsEnabled { get; set; }

        /// <summary>
        /// Whether device had external/network connection at event time.
        /// False = may explain image upload issues or inability to capture.
        /// </summary>
        public bool? DeviceOnline { get; set; }


        /// <summary>
        /// User-applied or AI-suggested tags for this event.
        /// May include abuse categorizations or threat level indicators.
        /// </summary>
        public List<string>? EventTags { get; set; }

        /// <summary>
        /// AI confidence in the detection model results (0.0 to 1.0).
        /// Lower confidence may indicate unclear evidence.
        /// </summary>
        public double? ModelConfidence { get; set; }

        /// <summary>
        /// Ring AI model version used for analysis.
        /// Important for reproducibility and understanding detection behavior.
        /// </summary>
        public string? AiModelVersion { get; set; }

        /// <summary>
        /// Whether evidence needs review due to signal/connectivity issues.
        /// CRITICAL: indicates potential tampering, jamming, or device interference.
        /// </summary>
        public bool NeedsReview { get; set; }

        /// <summary>
        /// Reason why evidence needs review (low signal, packet loss, etc.).
        /// </summary>
        public string? NeedsReviewReason { get; set; }

        /// <summary>
        /// Signal strength (RSSI) in dBm at time of event.
        /// Lower (more negative) values indicate weaker signal.
        /// -70 dBm or lower may indicate interference or tampering.
        /// </summary>
        public int? RssiDbm { get; set; }

        /// <summary>
        /// Packet loss percentage at time of event.
        /// Higher values indicate network instability or intentional interference.
        /// </summary>
        public double? PacketLossPercent { get; set; }
    }

    /// <summary>
    /// A recognized person profile detected in a snapshot.
    /// Important for identifying perpetrators and witnesses in DV cases.
    /// </summary>
    public class DetectedProfile
    {
        /// <summary>
        /// Profile ID in Ring system.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Name of the recognized person.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Confidence score for this identification (0.0 to 1.0).
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// URL to thumbnail image of the detected profile.
        /// </summary>
        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// A motion detection zone within the image frame.
    /// </summary>
    public class MotionZone
    {
        /// <summary>
        /// Zone ID identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Human-readable name for the zone (e.g., "front door", "living room").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Confidence score for motion in this zone (0.0 to 1.0).
        /// </summary>
        public double? Confidence { get; set; }
    }
}
