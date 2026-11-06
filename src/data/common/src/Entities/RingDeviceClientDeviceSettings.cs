namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.client_device_settings object (client eligibility and display flags). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceClientDeviceSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.client_device_settings.advanced_motion_enabled.</summary>
        public bool? AdvancedMotionEnabled { get; set; }

        /// <summary>settings.client_device_settings.cfes_eligible.</summary>
        public bool? CfesEligible { get; set; }

        /// <summary>settings.client_device_settings.dynamic_network_switching_eligible.</summary>
        public bool? DynamicNetworkSwitchingEligible { get; set; }

        /// <summary>settings.client_device_settings.light_schedule_enabled.</summary>
        public bool? LightScheduleEnabled { get; set; }

        /// <summary>settings.client_device_settings.local_playback_enabled.</summary>
        public bool? LocalPlaybackEnabled { get; set; }

        /// <summary>settings.client_device_settings.motion_message_enabled.</summary>
        public bool? MotionMessageEnabled { get; set; }

        /// <summary>settings.client_device_settings.motion_zone_recommendation.</summary>
        public bool? MotionZoneRecommendation { get; set; }

        /// <summary>settings.client_device_settings.night_vision_enabled.</summary>
        public bool? NightVisionEnabled { get; set; }

        /// <summary>settings.client_device_settings.people_only_enabled.</summary>
        public bool? PeopleOnlyEnabled { get; set; }

        /// <summary>settings.client_device_settings.ptz_setup_complete.</summary>
        public bool? PtzSetupComplete { get; set; }

        /// <summary>settings.client_device_settings.rich_notifications_eligible.</summary>
        public bool? RichNotificationsEligible { get; set; }

        /// <summary>settings.client_device_settings.ringtones_enabled.</summary>
        public bool? RingtonesEnabled { get; set; }

        /// <summary>settings.client_device_settings.shadow_correction_enabled.</summary>
        public bool? ShadowCorrectionEnabled { get; set; }

        /// <summary>settings.client_device_settings.sheila_camera_eligible.</summary>
        public bool? SheilaCameraEligible { get; set; }

        /// <summary>settings.client_device_settings.sheila_camera_processing_eligible.</summary>
        public bool? SheilaCameraProcessingEligible { get; set; }

        /// <summary>settings.client_device_settings.show_24x7_lite.</summary>
        public bool? Show24x7Lite { get; set; }

        /// <summary>settings.client_device_settings.show_offline_motion_events.</summary>
        public bool? ShowOfflineMotionEvents { get; set; }

        /// <summary>settings.client_device_settings.show_radar_data.</summary>
        public bool? ShowRadarData { get; set; }

        /// <summary>settings.client_device_settings.vehicle_detection_eligible.</summary>
        public bool? VehicleDetectionEligible { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
