namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.motion_settings object (advanced motion, PIR zone sensitivities, audio and recording flags; the snooze profile array is on RingDeviceMotionSnoozeProfiles). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceMotionSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_detection_enabled.</summary>
        public bool? AdvancedMotionDetectionEnabled { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_detection_human_only_mode.</summary>
        public bool? AdvancedMotionDetectionHumanOnlyMode { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_detection_loitering_mode.</summary>
        public bool? AdvancedMotionDetectionLoiteringMode { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_detection_mode.</summary>
        public string? AdvancedMotionDetectionMode { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_glance_enabled.</summary>
        public bool? AdvancedMotionGlanceEnabled { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_recording_human_mode.</summary>
        public bool? AdvancedMotionRecordingHumanMode { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_zones_enabled.</summary>
        public bool? AdvancedMotionZonesEnabled { get; set; }

        /// <summary>settings.motion_settings.advanced_motion_zones_type.</summary>
        public string? AdvancedMotionZonesType { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone1_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone1Sensitivity { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone2_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone2Sensitivity { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone3_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone3Sensitivity { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone4_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone4Sensitivity { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone5_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone5Sensitivity { get; set; }

        /// <summary>settings.motion_settings.advanced_pir_motion_zones.zone6_sensitivity.</summary>
        public int? AdvancedPirMotionZonesZone6Sensitivity { get; set; }

        /// <summary>settings.motion_settings.enable_audio.</summary>
        public bool? EnableAudio { get; set; }

        /// <summary>settings.motion_settings.enable_ir.</summary>
        public int? EnableIr { get; set; }

        /// <summary>settings.motion_settings.enable_recording.</summary>
        public bool? EnableRecording { get; set; }

        /// <summary>settings.motion_settings.end_detection.</summary>
        public int? EndDetection { get; set; }

        /// <summary>settings.motion_settings.loitering_threshold.</summary>
        public int? LoiteringThreshold { get; set; }

        /// <summary>settings.motion_settings.motion_sensitivity_profile.</summary>
        public int? MotionSensitivityProfile { get; set; }

        /// <summary>settings.motion_settings.motion_snooze_device_enabled.</summary>
        public bool? MotionSnoozeDeviceEnabled { get; set; }

        /// <summary>settings.motion_settings.motion_snooze_threshold_mins.</summary>
        public int? MotionSnoozeThresholdMins { get; set; }

        /// <summary>settings.motion_settings.zone_settings_v2_enabled.</summary>
        public bool? ZoneSettingsV2Enabled { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
