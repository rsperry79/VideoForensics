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

        /// <summary>settings.backend_settings.paid_features.alarm_triggered_recording.</summary>
        public bool? PaidFeaturesAlarmTriggeredRecording { get; set; }

        /// <summary>settings.backend_settings.paid_features.alexa_concierge.</summary>
        public bool? PaidFeaturesAlexaConcierge { get; set; }

        /// <summary>settings.backend_settings.paid_features.animal.</summary>
        public bool? PaidFeaturesAnimal { get; set; }

        /// <summary>settings.backend_settings.paid_features.appstore.</summary>
        public bool? PaidFeaturesAppstore { get; set; }

        /// <summary>settings.backend_settings.paid_features.baby_cry.</summary>
        public bool? PaidFeaturesBabyCry { get; set; }

        /// <summary>settings.backend_settings.paid_features.car_alarm.</summary>
        public bool? PaidFeaturesCarAlarm { get; set; }

        /// <summary>settings.backend_settings.paid_features.closed_caption.</summary>
        public bool? PaidFeaturesClosedCaption { get; set; }

        /// <summary>settings.backend_settings.paid_features.co2_smoke_alarm.</summary>
        public bool? PaidFeaturesCo2SmokeAlarm { get; set; }

        /// <summary>settings.backend_settings.paid_features.critical_alerts.</summary>
        public bool? PaidFeaturesCriticalAlerts { get; set; }

        /// <summary>settings.backend_settings.paid_features.cv_triggers.</summary>
        public bool? PaidFeaturesCvTriggers { get; set; }

        /// <summary>settings.backend_settings.paid_features.daily_digest.</summary>
        public bool? PaidFeaturesDailyDigest { get; set; }

        /// <summary>settings.backend_settings.paid_features.ding_call.</summary>
        public bool? PaidFeaturesDingCall { get; set; }

        /// <summary>settings.backend_settings.paid_features.dog_bark.</summary>
        public bool? PaidFeaturesDogBark { get; set; }

        /// <summary>settings.backend_settings.paid_features.extended_live_view.</summary>
        public bool? PaidFeaturesExtendedLiveView { get; set; }

        /// <summary>settings.backend_settings.paid_features.general_sound.</summary>
        public bool? PaidFeaturesGeneralSound { get; set; }

        /// <summary>settings.backend_settings.paid_features.glass_break.</summary>
        public bool? PaidFeaturesGlassBreak { get; set; }

        /// <summary>settings.backend_settings.paid_features.human.</summary>
        public bool? PaidFeaturesHuman { get; set; }

        /// <summary>settings.backend_settings.paid_features.live_speak.</summary>
        public bool? PaidFeaturesLiveSpeak { get; set; }

        /// <summary>settings.backend_settings.paid_features.loitering.</summary>
        public bool? PaidFeaturesLoitering { get; set; }

        /// <summary>settings.backend_settings.paid_features.motion.</summary>
        public bool? PaidFeaturesMotion { get; set; }

        /// <summary>settings.backend_settings.paid_features.motion_awareness.</summary>
        public bool? PaidFeaturesMotionAwareness { get; set; }

        /// <summary>settings.backend_settings.paid_features.motion_stop.</summary>
        public bool? PaidFeaturesMotionStop { get; set; }

        /// <summary>settings.backend_settings.paid_features.multicam_live_view.</summary>
        public bool? PaidFeaturesMulticamLiveView { get; set; }

        /// <summary>settings.backend_settings.paid_features.natural_language_search.</summary>
        public bool? PaidFeaturesNaturalLanguageSearch { get; set; }

        /// <summary>settings.backend_settings.paid_features.other_motion.</summary>
        public bool? PaidFeaturesOtherMotion { get; set; }

        /// <summary>settings.backend_settings.paid_features.package_delivery.</summary>
        public bool? PaidFeaturesPackageDelivery { get; set; }

        /// <summary>settings.backend_settings.paid_features.package_pickup.</summary>
        public bool? PaidFeaturesPackagePickup { get; set; }

        /// <summary>settings.backend_settings.paid_features.package_protection.</summary>
        public bool? PaidFeaturesPackageProtection { get; set; }

        /// <summary>settings.backend_settings.paid_features.person_identification.</summary>
        public bool? PaidFeaturesPersonIdentification { get; set; }

        /// <summary>settings.backend_settings.paid_features.recording_24x7.</summary>
        public bool? PaidFeaturesRecording24x7 { get; set; }

        /// <summary>settings.backend_settings.paid_features.retinal_tuning.</summary>
        public bool? PaidFeaturesRetinalTuning { get; set; }

        /// <summary>settings.backend_settings.paid_features.rogers_vqa.</summary>
        public bool? PaidFeaturesRogersVqa { get; set; }

        /// <summary>settings.backend_settings.paid_features.sheila_cv.</summary>
        public bool? PaidFeaturesSheilaCv { get; set; }

        /// <summary>settings.backend_settings.paid_features.sheila_recording.</summary>
        public bool? PaidFeaturesSheilaRecording { get; set; }

        /// <summary>settings.backend_settings.paid_features.smart_media_descriptions.</summary>
        public bool? PaidFeaturesSmartMediaDescriptions { get; set; }

        /// <summary>settings.backend_settings.paid_features.snapshot_capture_plus.</summary>
        public bool? PaidFeaturesSnapshotCapturePlus { get; set; }

        /// <summary>settings.backend_settings.paid_features.system_level_pip.</summary>
        public bool? PaidFeaturesSystemLevelPip { get; set; }

        /// <summary>settings.backend_settings.paid_features.unverified_motion.</summary>
        public bool? PaidFeaturesUnverifiedMotion { get; set; }

        /// <summary>settings.backend_settings.paid_features.vehicle.</summary>
        public bool? PaidFeaturesVehicle { get; set; }

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
