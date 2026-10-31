namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Device capability flags normalized from provider responses (P1 entity for provider metadata normalization).</summary>
    public class DeviceFeatures
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public bool? MotionsEnabled { get; set; }
        public bool? ShowRecordings { get; set; }
        public bool? AdvancedMotionEnabled { get; set; }
        public bool? PeopleOnlyEnabled { get; set; }
        public bool? ShadowCorrectionEnabled { get; set; }
        public bool? MotionMessageEnabled { get; set; }
        public bool? NightVisionEnabled { get; set; }

        /// <summary>Stickup cam show_vod_settings feature flag.</summary>
        public bool? ShowVodSettings { get; set; }

        /// <summary>Chime ringtones_enabled feature flag.</summary>
        public bool? RingtonesEnabled { get; set; }

        // Scalar leaves directly under features. All are bool?; the ones that were null in every observed sample (package_warning, auto_zoom_track,
        // automated_siren, alexa_plus_greetings, remote_access_control, auto_shutoff_device, short_on_long_off, tracklight, animal_detection_eligible,
        // recording_24x7_eligible, property_view) have an unconfirmed type, so bool? is a best guess. property_view arrives as an object in some
        // samples; its enablement is stored as a DeviceFeatureState row and this column only holds a plain boolean value.

        /// <summary>Ring features.cfes_eligible.</summary>
        public bool? CfesEligible { get; set; }

        /// <summary>Ring features.package_warning.</summary>
        public bool? PackageWarning { get; set; }

        /// <summary>Ring features.auto_zoom_track.</summary>
        public bool? AutoZoomTrack { get; set; }

        /// <summary>Ring features.automated_siren.</summary>
        public bool? AutomatedSiren { get; set; }

        /// <summary>Ring features.alexa_plus_greetings.</summary>
        public bool? AlexaPlusGreetings { get; set; }

        /// <summary>Ring features.remote_access_control.</summary>
        public bool? RemoteAccessControl { get; set; }

        /// <summary>Ring features.auto_shutoff_device.</summary>
        public bool? AutoShutoffDevice { get; set; }

        /// <summary>Ring features.short_on_long_off.</summary>
        public bool? ShortOnLongOff { get; set; }

        /// <summary>Ring features.tracklight.</summary>
        public bool? Tracklight { get; set; }

        /// <summary>Ring features.motion_zone_recommendation.</summary>
        public bool? MotionZoneRecommendation { get; set; }

        /// <summary>Ring features.rich_notifications_eligible.</summary>
        public bool? RichNotificationsEligible { get; set; }

        /// <summary>Ring features.show_offline_motion_events.</summary>
        public bool? ShowOfflineMotionEvents { get; set; }

        /// <summary>Ring features.sheila_camera_eligible.</summary>
        public bool? SheilaCameraEligible { get; set; }

        /// <summary>Ring features.sheila_camera_processing_eligible.</summary>
        public bool? SheilaCameraProcessingEligible { get; set; }

        /// <summary>Ring features.dynamic_network_switching_eligible.</summary>
        public bool? DynamicNetworkSwitchingEligible { get; set; }

        /// <summary>Ring features.vehicle_detection_eligible.</summary>
        public bool? VehicleDetectionEligible { get; set; }

        /// <summary>Ring features.animal_detection_eligible.</summary>
        public bool? AnimalDetectionEligible { get; set; }

        /// <summary>Ring features.show_24x7_lite.</summary>
        public bool? Show24x7Lite { get; set; }

        /// <summary>Ring features.recording_24x7_eligible.</summary>
        public bool? Recording24x7Eligible { get; set; }

        /// <summary>Ring features.property_view.</summary>
        public bool? PropertyView { get; set; }
    }
}
