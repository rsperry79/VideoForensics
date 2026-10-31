namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One Ring feature object from a device's <c>features</c> (e.g. "smart_video_search"): one row per device per feature key,
    /// replaced on every sync. Every value is a typed column; null means Ring did not send that part of the object.
    /// </summary>
    public class RingDeviceFeatureState
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Ring's feature key, e.g. "smart_video_search", "video_recording".</summary>
        public string FeatureKey { get; set; } = string.Empty;

        /// <summary>eligibility.eligible.</summary>
        public bool? Eligible { get; set; }

        /// <summary>enablement.allowed.</summary>
        public bool? Allowed { get; set; }

        /// <summary>enablement.enabled.</summary>
        public bool? Enabled { get; set; }

        /// <summary>original_video_quality_download_offer.is_enabled.</summary>
        public bool? IsEnabled { get; set; }

        /// <summary>chime_settings.is_eligible.</summary>
        public bool? IsEligible { get; set; }

        /// <summary>ai_labs_* allows_no_location.</summary>
        public bool? AllowsNoLocation { get; set; }

        /// <summary>video_rendering.max_digital_zoom_level.</summary>
        public double? MaxDigitalZoomLevel { get; set; }

        /// <summary>retinal_tuning.rt_max_digital_zoom_level.</summary>
        public double? RtMaxDigitalZoomLevel { get; set; }

        /// <summary>transformer_score.score.</summary>
        public string? Score { get; set; }

        /// <summary>unusual_alert.learning. Null in every observed sample, so the type is a best guess (text; bool/number values are stored as invariant text).</summary>
        public string? Learning { get; set; }

        /// <summary>video_recording.recording_mode (e.g. "motion_based").</summary>
        public string? RecordingMode { get; set; }

        /// <summary>video_recording.recording_enabled.</summary>
        public bool? RecordingEnabled { get; set; }

        /// <summary>video_recording.recording_state (e.g. "active").</summary>
        public string? RecordingState { get; set; }

        /// <summary>video_recording.recording_mode_availability.continuous.supported.</summary>
        public bool? ContinuousSupported { get; set; }

        /// <summary>video_recording.recording_mode_availability.continuous.available. Null in every observed sample.</summary>
        public bool? ContinuousAvailable { get; set; }

        /// <summary>video_recording.recording_mode_availability.continuous.unavailability_reason.</summary>
        public string? ContinuousUnavailabilityReason { get; set; }

        /// <summary>The ineligibility, disallow and affected-feature reason lists of this feature.</summary>
        public List<RingDeviceFeatureReason> Reasons { get; set; } = [];
    }
}
