using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Complete record of a Ring event download with enriched metadata, device telemetry,
    /// AI analysis results, and file information. Written by the client application after
    /// successful download completion.
    /// </summary>
    public class DownloadedEventRecord
    {
        /// <summary>
        /// Metadata about the event and download
        /// </summary>
        [JsonPropertyName("event")]
        public DownloadEventMetadata Event { get; set; }

        /// <summary>
        /// File information (path, size, hash, etc.)
        /// </summary>
        [JsonPropertyName("file")]
        public DownloadedFileInfo File { get; set; }

        /// <summary>
        /// Device health and telemetry at time of event
        /// </summary>
        [JsonPropertyName("device_health")]
        public DeviceHealthSnapshot DeviceHealth { get; set; }

        /// <summary>
        /// Device configuration and settings at time of event (optional)
        /// </summary>
        [JsonPropertyName("device_config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DeviceConfigSnapshot DeviceConfig { get; set; }

        /// <summary>
        /// Camera/device information
        /// </summary>
        [JsonPropertyName("device")]
        public DeviceSnapshot Device { get; set; }

        /// <summary>
        /// Location information (optional - depends on configuration)
        /// </summary>
        [JsonPropertyName("location")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LocationSnapshot Location { get; set; }

        /// <summary>
        /// Account/profile information (optional - depends on configuration)
        /// </summary>
        [JsonPropertyName("account")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AccountSnapshot Account { get; set; }

        /// <summary>
        /// AI/Computer Vision analysis results
        /// </summary>
        [JsonPropertyName("ai_analysis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AiAnalysisSnapshot AiAnalysis { get; set; }

        /// <summary>
        /// Download processing metadata
        /// </summary>
        [JsonPropertyName("download")]
        public DownloadProcessingInfo Download { get; set; }

        /// <summary>
        /// Video metadata processing results (validation and correction status)
        /// </summary>
        [JsonPropertyName("metadata_processing")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MetadataProcessingInfo? MetadataProcessing { get; set; }
    }

    /// <summary>
    /// Video metadata processing and validation results
    /// </summary>
    public class MetadataProcessingInfo
    {
        /// <summary>
        /// Overall status of metadata processing
        /// </summary>
        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MetadataStatus Status { get; set; }

        /// <summary>
        /// Whether metadata was successfully written to the file
        /// </summary>
        [JsonPropertyName("was_written")]
        public bool WasWritten { get; set; }

        /// <summary>
        /// Whether the file passed validation after processing
        /// </summary>
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether the file was corrected (codec, frame rate, etc.)
        /// </summary>
        [JsonPropertyName("was_corrected")]
        public bool WasCorrected { get; set; }

        /// <summary>
        /// List of corrections applied to the file
        /// </summary>
        [JsonPropertyName("corrections_applied")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? CorrectionsApplied { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        [JsonPropertyName("error_message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Duration of metadata processing in milliseconds
        /// </summary>
        [JsonPropertyName("duration_ms")]
        public long DurationMs { get; set; }

        /// <summary>
        /// Timestamp when processing completed
        /// </summary>
        [JsonPropertyName("processed_at")]
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// PhotoPrism-compatible tags written to the file
        /// </summary>
        [JsonPropertyName("photoprism_tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? PhotoPrismTags { get; set; }
    }

    /// <summary>
    /// Metadata status enumeration matching VideoForensics.Providers.Ring.Video.Metadata
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MetadataStatus
    {
        NotProcessed = 0,
        Valid = 1,
        Corrected = 2,
        Corrupt = 3,
        Failed = 4
    }

    /// <summary>
    /// Event-level metadata
    /// </summary>
    public class DownloadEventMetadata
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("answered")]
        public bool Answered { get; set; }

        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }

        [JsonPropertyName("snapshot_url")]
        public string SnapshotUrl { get; set; }

        [JsonPropertyName("recording_status")]
        public string RecordingStatus { get; set; }
    }

    /// <summary>
    /// Downloaded file information
    /// </summary>
    public class DownloadedFileInfo
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("last_modified")]
        public DateTime LastModified { get; set; }

        [JsonPropertyName("sha256_hash")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Sha256Hash { get; set; }

        [JsonPropertyName("video_codec")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string VideoCodec { get; set; }

        [JsonPropertyName("audio_codec")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AudioCodec { get; set; }

        [JsonPropertyName("resolution")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Resolution { get; set; }

        [JsonPropertyName("frame_rate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FrameRate { get; set; }
    }

    /// <summary>
    /// Device health and telemetry snapshot at event time
    /// </summary>
    public class DeviceHealthSnapshot
    {
        [JsonPropertyName("connected")]
        public bool? Connected { get; set; }

        [JsonPropertyName("battery_percentage")]
        public int? BatteryPercentage { get; set; }

        [JsonPropertyName("battery_percentage_category")]
        public string BatteryPercentageCategory { get; set; }

        [JsonPropertyName("battery_voltage")]
        public decimal? BatteryVoltage { get; set; }

        [JsonPropertyName("battery_voltage_category")]
        public string BatteryVoltageCategory { get; set; }

        [JsonPropertyName("battery_present")]
        public bool? BatteryPresent { get; set; }

        [JsonPropertyName("external_power_connected")]
        public bool? ExternalPowerConnected { get; set; }

        [JsonPropertyName("rssi")]
        public double? Rssi { get; set; }

        [JsonPropertyName("rssi_category")]
        public string RssiCategory { get; set; }

        [JsonPropertyName("latest_signal_strength")]
        public double? LatestSignalStrength { get; set; }

        [JsonPropertyName("latest_signal_category")]
        public string LatestSignalCategory { get; set; }

        [JsonPropertyName("average_signal_strength")]
        public double? AverageSignalStrength { get; set; }

        [JsonPropertyName("average_signal_category")]
        public string AverageSignalCategory { get; set; }

        [JsonPropertyName("packet_loss")]
        public double? PacketLoss { get; set; }

        [JsonPropertyName("packet_loss_category")]
        public string PacketLossCategory { get; set; }

        [JsonPropertyName("wifi_name")]
        public string WifiName { get; set; }

        [JsonPropertyName("wifi_is_ring_network")]
        public bool? WifiIsRingNetwork { get; set; }

        [JsonPropertyName("firmware_version")]
        public string FirmwareVersion { get; set; }

        [JsonPropertyName("firmware_version_status")]
        public string FirmwareVersionStatus { get; set; }

        [JsonPropertyName("ota_status")]
        public string OtaStatus { get; set; }

        [JsonPropertyName("last_update_time")]
        public DateTime? LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Device configuration and feature enablement at event time (optional)
    /// </summary>
    public class DeviceConfigSnapshot
    {
        [JsonPropertyName("motion_detection_enabled")]
        public bool? MotionDetectionEnabled { get; set; }

        [JsonPropertyName("advanced_motion_enabled")]
        public bool? AdvancedMotionEnabled { get; set; }

        [JsonPropertyName("people_only_detection_enabled")]
        public bool? PeopleOnlyDetectionEnabled { get; set; }

        [JsonPropertyName("night_vision_enabled")]
        public bool? NightVisionEnabled { get; set; }

        [JsonPropertyName("show_recordings_enabled")]
        public bool? ShowRecordingsEnabled { get; set; }

        [JsonPropertyName("shadow_correction_enabled")]
        public bool? ShadowCorrectionEnabled { get; set; }

        [JsonPropertyName("motion_message_enabled")]
        public bool? MotionMessageEnabled { get; set; }

        [JsonPropertyName("subscribed_to_notifications")]
        public bool? SubscribedToNotifications { get; set; }

        [JsonPropertyName("subscribed_to_motions")]
        public bool? SubscribedToMotions { get; set; }
    }

    /// <summary>
    /// Device snapshot (camera/doorbell info)
    /// </summary>
    public class DeviceSnapshot
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("firmware_version")]
        public string FirmwareVersion { get; set; }

        [JsonPropertyName("owned")]
        public bool? Owned { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }
    }

    /// <summary>
    /// Location snapshot (optional - depends on configuration)
    /// </summary>
    public class LocationSnapshot
    {
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("time_zone")]
        public string TimeZone { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("owner")]
        public OwnerSnapshot Owner { get; set; }
    }

    /// <summary>
    /// Location owner info
    /// </summary>
    public class OwnerSnapshot
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }
    }

    /// <summary>
    /// Account/user snapshot (optional - depends on configuration)
    /// </summary>
    public class AccountSnapshot
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("phone_number")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PhoneNumber { get; set; }

        [JsonPropertyName("subscription_level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SubscriptionLevel { get; set; }
    }

    /// <summary>
    /// AI/Computer Vision analysis results
    /// </summary>
    public class AiAnalysisSnapshot
    {
        [JsonPropertyName("person_detected")]
        public bool? PersonDetected { get; set; }

        [JsonPropertyName("detection_type")]
        public string DetectionType { get; set; }

        [JsonPropertyName("detection_types")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> DetectionTypes { get; set; }

        [JsonPropertyName("confidence_score")]
        public double? ConfidenceScore { get; set; }

        [JsonPropertyName("anomaly_score")]
        public double? AnomalyScore { get; set; }

        [JsonPropertyName("stream_quality_broken")]
        public bool? StreamQualityBroken { get; set; }

        [JsonPropertyName("full_description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FullDescription { get; set; }

        [JsonPropertyName("short_description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ShortDescription { get; set; }

        [JsonPropertyName("recognized_persons")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RecognizedPersonSnapshot> RecognizedPersons { get; set; }

        [JsonPropertyName("security_alerts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SecurityAlerts { get; set; }

        [JsonPropertyName("motion_zones")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MotionZoneSnapshot> MotionZones { get; set; }

        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Tags { get; set; }
    }

    /// <summary>
    /// Recognized person in AI analysis
    /// </summary>
    public class RecognizedPersonSnapshot
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("thumbnail_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ThumbnailUrl { get; set; }
    }

    /// <summary>
    /// Motion detection zone with confidence
    /// </summary>
    public class MotionZoneSnapshot
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }

    /// <summary>
    /// Download processing metadata
    /// </summary>
    public class DownloadProcessingInfo
    {
        [JsonPropertyName("download_start")]
        public DateTime DownloadStart { get; set; }

        [JsonPropertyName("download_end")]
        public DateTime DownloadEnd { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("attempts")]
        public int Attempts { get; set; }

        [JsonPropertyName("error_message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("retry_count")]
        public int RetryCount { get; set; }

        [JsonPropertyName("downloaded_by_version")]
        public string DownloadedByVersion { get; set; }

        [JsonPropertyName("application")]
        public string Application { get; set; }
    }
}
