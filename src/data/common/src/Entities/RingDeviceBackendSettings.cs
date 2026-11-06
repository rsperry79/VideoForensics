namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.backend_settings object (notification, live view, paid-feature and terms flags). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceBackendSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.backend_settings.enable_rich_notifications.</summary>
        public bool? EnableRichNotifications { get; set; }

        /// <summary>settings.backend_settings.live_view_disabled.</summary>
        public bool? LiveViewDisabled { get; set; }

        /// <summary>settings.backend_settings.live_view_preset_profile.</summary>
        public string? LiveViewPresetProfile { get; set; }

        /// <summary>settings.backend_settings.motion_snooze_preset_profile.</summary>
        public string? MotionSnoozePresetProfile { get; set; }

        /// <summary>settings.backend_settings.motion_warning_enabled.</summary>
        public bool? MotionWarningEnabled { get; set; }

        /// <summary>settings.backend_settings.paid_features.ai_alexa_plus_greetings.</summary>
        public bool? PaidFeaturesAiAlexaPlusGreetings { get; set; }

        /// <summary>settings.backend_settings.paid_features.ai_automated_warnings.</summary>
        public bool? PaidFeaturesAiAutomatedWarnings { get; set; }

        /// <summary>settings.backend_settings.paid_features.ai_labs_daily_clips.</summary>
        public bool? PaidFeaturesAiLabsDailyClips { get; set; }

        /// <summary>settings.backend_settings.paid_features.ai_labs_memorable_moments.</summary>
        public bool? PaidFeaturesAiLabsMemorableMoments { get; set; }

        /// <summary>settings.backend_settings.paid_features.appstore.</summary>
        public bool? PaidFeaturesAppstore { get; set; }

        /// <summary>settings.backend_settings.paid_features.motion_awareness.</summary>
        public bool? PaidFeaturesMotionAwareness { get; set; }

        /// <summary>settings.backend_settings.paid_features.motion_stop.</summary>
        public bool? PaidFeaturesMotionStop { get; set; }

        /// <summary>settings.backend_settings.paid_features.person_identification.</summary>
        public bool? PaidFeaturesPersonIdentification { get; set; }

        /// <summary>settings.backend_settings.paid_features.retinal_tuning.</summary>
        public bool? PaidFeaturesRetinalTuning { get; set; }

        /// <summary>settings.backend_settings.paid_features.rogers_vqa.</summary>
        public bool? PaidFeaturesRogersVqa { get; set; }

        /// <summary>settings.backend_settings.paid_features.smart_media_descriptions.</summary>
        public bool? PaidFeaturesSmartMediaDescriptions { get; set; }

        /// <summary>settings.backend_settings.paid_features.unverified_motion.</summary>
        public bool? PaidFeaturesUnverifiedMotion { get; set; }

        /// <summary>settings.backend_settings.terms_of_service_accepted.autoreply.</summary>
        public bool? TermsOfServiceAcceptedAutoreply { get; set; }

        /// <summary>settings.backend_settings.terms_of_service_accepted.concierge.</summary>
        public bool? TermsOfServiceAcceptedConcierge { get; set; }

        /// <summary>settings.backend_settings.user_specified_footage_hours_ttl.</summary>
        public int? UserSpecifiedFootageHoursTtl { get; set; }

        /// <summary>settings.backend_settings.user_specified_recording_ttl.</summary>
        public int? UserSpecifiedRecordingTtl { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
