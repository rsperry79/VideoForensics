using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Response of GET .../locations/{id}/history/evm (the evm location history feed), one page of entries.
    /// The cek_transfer_recipient_response field is null in every capture and has no known shape, so it is not modelled.
    /// </summary>
    public class LocationHistoryResponse
    {
        [JsonPropertyName("items")]
        public List<LocationHistoryItem> Items { get; set; }

        [JsonPropertyName("pagination_key")]
        public string PaginationKey { get; set; }
    }

    /// <summary>One entry of the evm location history feed.</summary>
    public class LocationHistoryItem
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; }

        [JsonPropertyName("source_id")]
        public string SourceId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        /// <summary>Always null in captures; typed as text because no non-null value has been observed.</summary>
        [JsonPropertyName("source_type")]
        public string SourceType { get; set; }

        [JsonPropertyName("duration_ms")]
        public long? DurationMs { get; set; }

        [JsonPropertyName("session_duration")]
        public long? SessionDuration { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("had_subscription")]
        public bool? HadSubscription { get; set; }

        [JsonPropertyName("is_favorite")]
        public bool? IsFavorite { get; set; }

        [JsonPropertyName("recording_status")]
        public string RecordingStatus { get; set; }

        [JsonPropertyName("cv")]
        public LocationHistoryCv Cv { get; set; }

        [JsonPropertyName("properties")]
        public LocationEventProperties Properties { get; set; }

        /// <summary>Always null in captures; typed as text because no non-null value has been observed.</summary>
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("origin_display_name")]
        public string OriginDisplayName { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonPropertyName("visualizations")]
        public LocationHistoryVisualizations Visualizations { get; set; }

        /// <summary>The device the entry belongs to (id, description and type).</summary>
        [JsonPropertyName("device")]
        public LocationEventDoorbot Device { get; set; }

        [JsonPropertyName("owner_id")]
        public string OwnerId { get; set; }

        [JsonPropertyName("delete_in_progress")]
        public bool? DeleteInProgress { get; set; }

        /// <summary>Null in every capture; typed by name because no non-null value has been observed.</summary>
        [JsonPropertyName("low_confidence")]
        public bool? LowConfidence { get; set; }

        /// <summary>Null in every capture; typed by name because no non-null value has been observed.</summary>
        [JsonPropertyName("similarity")]
        public double? Similarity { get; set; }

        [JsonPropertyName("riid")]
        public string Riid { get; set; }

        /// <summary>Null in every capture; typed by name because no non-null value has been observed.</summary>
        [JsonPropertyName("user_specified_recording_disabled")]
        public bool? UserSpecifiedRecordingDisabled { get; set; }

        [JsonPropertyName("is_watched")]
        public bool? IsWatched { get; set; }

        /// <summary>Null in every capture; typed by name because no non-null value has been observed.</summary>
        [JsonPropertyName("is_property_view_enabled")]
        public bool? IsPropertyViewEnabled { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("user_action_disabled")]
        public bool? UserActionDisabled { get; set; }

        /// <summary>Null in every capture; typed as text because no non-null value has been observed.</summary>
        [JsonPropertyName("mission")]
        public string Mission { get; set; }
    }

    /// <summary>Computer vision properties of a location history entry. detection_details is a list here, not an object.</summary>
    public class LocationHistoryCv
    {
        [JsonPropertyName("person_detected")]
        public bool? PersonDetected { get; set; }

        [JsonPropertyName("stream_broken")]
        public bool? StreamBroken { get; set; }

        [JsonPropertyName("detection_type")]
        public string DetectionType { get; set; }

        [JsonPropertyName("detection_types")]
        public List<CvDetectionType> DetectionTypes { get; set; }

        [JsonPropertyName("security_alerts")]
        public CvSecurityAlerts SecurityAlerts { get; set; }

        [JsonPropertyName("profiles")]
        public List<CvProfile> Profiles { get; set; }

        [JsonPropertyName("full_description")]
        public string FullDescription { get; set; }

        [JsonPropertyName("short_description")]
        public string ShortDescription { get; set; }

        [JsonPropertyName("similarity")]
        public double? Similarity { get; set; }

        [JsonPropertyName("anomaly")]
        public double? Anomaly { get; set; }

        [JsonPropertyName("detection_details")]
        public List<LocationHistoryDetectionDetail> DetectionDetails { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }

    /// <summary>One entry of cv.detection_details. objects_per_lens is null in every capture and has no known shape.</summary>
    public class LocationHistoryDetectionDetail
    {
        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>The visualizations of an entry. The radar, map and potential gap visualizations are null in every capture and have no known shape.</summary>
    public class LocationHistoryVisualizations
    {
        [JsonPropertyName("cloud_media_visualization")]
        public LocationHistoryMediaVisualization CloudMediaVisualization { get; set; }

        [JsonPropertyName("local_media_visualization")]
        public LocationHistoryMediaVisualization LocalMediaVisualization { get; set; }
    }

    /// <summary>A cloud or local media visualization: a schema name and a list of media items.</summary>
    public class LocationHistoryMediaVisualization
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; }

        [JsonPropertyName("media")]
        public List<LocationHistoryMedia> Media { get; set; }
    }

    /// <summary>One media item of a visualization.</summary>
    public class LocationHistoryMedia
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }

        [JsonPropertyName("custom_metadata")]
        public LocationHistoryMediaMetadata CustomMetadata { get; set; }

        [JsonPropertyName("is_e2ee")]
        public bool? IsE2ee { get; set; }

        /// <summary>Null in every capture; typed as text because no non-null value has been observed.</summary>
        [JsonPropertyName("manifest_id")]
        public string ManifestId { get; set; }

        [JsonPropertyName("file_type")]
        public string FileType { get; set; }

        [JsonPropertyName("file_family")]
        public string FileFamily { get; set; }

        [JsonPropertyName("preroll_duration_ms")]
        public long? PrerollDurationMs { get; set; }

        [JsonPropertyName("playback_duration")]
        public long? PlaybackDuration { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    /// <summary>The custom_metadata object of a media item.</summary>
    public class LocationHistoryMediaMetadata
    {
        [JsonPropertyName("recording_24x7_enabled")]
        public bool? Recording24x7Enabled { get; set; }

        [JsonPropertyName("ding_id")]
        public string DingId { get; set; }

        [JsonPropertyName("tracks")]
        public List<LocationHistoryMediaTrack> Tracks { get; set; }

        [JsonPropertyName("audio_encoding")]
        public string AudioEncoding { get; set; }

        [JsonPropertyName("video_encoding")]
        public string VideoEncoding { get; set; }

        [JsonPropertyName("tba_transcoding_enabled")]
        public bool? TbaTranscodingEnabled { get; set; }
    }

    /// <summary>One track of custom_metadata.tracks.</summary>
    public class LocationHistoryMediaTrack
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("codec")]
        public string Codec { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
