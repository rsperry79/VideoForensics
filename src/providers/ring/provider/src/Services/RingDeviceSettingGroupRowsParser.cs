using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Parses the endpoint setting groups from one device's settings object: the 1:1 rows of the Ring group tables and misc table, and the motion snooze
    /// and cv threshold child rows. Scalar keys use the same helpers as <see cref="RingDeviceSettingsParser"/>, so a wrong JSON type is noted by key and
    /// kind only and stored as null, and a typed null is stored without a note. Child lists are always returned (empty when Ring sent no array), because
    /// each list is the complete current value.
    /// </summary>
    internal static class RingDeviceSettingGroupRowsParser
    {
        public static RingDeviceSettingGroupRows Parse(IReadOnlyDictionary<string, JsonElement> f, List<string> notes)
        {
            var advancedMotion = new RingDeviceAdvancedMotionSettings
            {
                Zone1Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_1", "name"),
                Zone1State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "state"),
                Zone1Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex1", "x"),
                Zone1Vertex1Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex1", "y"),
                Zone1Vertex2X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex2", "x"),
                Zone1Vertex2Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex2", "y"),
                Zone1Vertex3X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex3", "x"),
                Zone1Vertex3Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex3", "y"),
                Zone1Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex4", "x"),
                Zone1Vertex4Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex4", "y"),
                Zone1Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex5", "x"),
                Zone1Vertex5Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex5", "y"),
                Zone1Vertex6X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex6", "x"),
                Zone1Vertex6Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex6", "y"),
                Zone1Vertex7X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex7", "x"),
                Zone1Vertex7Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex7", "y"),
                Zone1Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex8", "x"),
                Zone1Vertex8Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex8", "y"),
                Zone2Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_2", "name"),
                Zone2State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "state"),
                Zone2Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex1", "x"),
                Zone2Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex1", "y"),
                Zone2Vertex2X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex2", "x"),
                Zone2Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex2", "y"),
                Zone2Vertex3X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex3", "x"),
                Zone2Vertex3Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex3", "y"),
                Zone2Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex4", "x"),
                Zone2Vertex4Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex4", "y"),
                Zone2Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex5", "x"),
                Zone2Vertex5Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex5", "y"),
                Zone2Vertex6X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex6", "x"),
                Zone2Vertex6Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex6", "y"),
                Zone2Vertex7X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex7", "x"),
                Zone2Vertex7Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex7", "y"),
                Zone2Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex8", "x"),
                Zone2Vertex8Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex8", "y"),
                Zone3Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_3", "name"),
                Zone3State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "state"),
                Zone3Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex1", "x"),
                Zone3Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex1", "y"),
                Zone3Vertex2X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex2", "x"),
                Zone3Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex2", "y"),
                Zone3Vertex3X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex3", "x"),
                Zone3Vertex3Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex3", "y"),
                Zone3Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex4", "x"),
                Zone3Vertex4Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex4", "y"),
                Zone3Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex5", "x"),
                Zone3Vertex5Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex5", "y"),
                Zone3Vertex6X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex6", "x"),
                Zone3Vertex6Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex6", "y"),
                Zone3Vertex7X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex7", "x"),
                Zone3Vertex7Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex7", "y"),
                Zone3Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex8", "x"),
                Zone3Vertex8Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex8", "y")
            };

            var backend = new RingDeviceBackendSettings
            {
                EnableRichNotifications = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "enable_rich_notifications"),
                LiveViewDisabled = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "live_view_disabled"),
                LiveViewPresetProfile = RingDeviceSettingsParser.Text(f, notes, "backend_settings", "live_view_preset_profile"),
                MotionSnoozePresetProfile = RingDeviceSettingsParser.Text(f, notes, "backend_settings", "motion_snooze_preset_profile"),
                MotionWarningEnabled = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "motion_warning_enabled"),
                PaidFeaturesAiAlexaPlusGreetings = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_alexa_plus_greetings"),
                PaidFeaturesAiAutomatedWarnings = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_automated_warnings"),
                PaidFeaturesAiLabsDailyClips = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_labs_daily_clips"),
                PaidFeaturesAiLabsMemorableMoments = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_labs_memorable_moments"),
                PaidFeaturesAppstore = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "appstore"),
                PaidFeaturesMotionAwareness = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "motion_awareness"),
                PaidFeaturesMotionStop = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "motion_stop"),
                PaidFeaturesPersonIdentification = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "person_identification"),
                PaidFeaturesRetinalTuning = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "retinal_tuning"),
                PaidFeaturesRogersVqa = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "rogers_vqa"),
                PaidFeaturesSmartMediaDescriptions = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "smart_media_descriptions"),
                PaidFeaturesUnverifiedMotion = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "unverified_motion"),
                TermsOfServiceAcceptedAutoreply = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "terms_of_service_accepted", "autoreply"),
                TermsOfServiceAcceptedConcierge = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "terms_of_service_accepted", "concierge"),
                UserSpecifiedFootageHoursTtl = RingDeviceSettingsParser.Int(f, notes, "backend_settings", "user_specified_footage_hours_ttl"),
                UserSpecifiedRecordingTtl = RingDeviceSettingsParser.Int(f, notes, "backend_settings", "user_specified_recording_ttl")
            };

            var clientDevice = new RingDeviceClientDeviceSettings
            {
                AdvancedMotionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "advanced_motion_enabled"),
                CfesEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "cfes_eligible"),
                DynamicNetworkSwitchingEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "dynamic_network_switching_eligible"),
                LightScheduleEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "light_schedule_enabled"),
                LocalPlaybackEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "local_playback_enabled"),
                MotionMessageEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "motion_message_enabled"),
                MotionZoneRecommendation = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "motion_zone_recommendation"),
                NightVisionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "night_vision_enabled"),
                PeopleOnlyEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "people_only_enabled"),
                PtzSetupComplete = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "ptz_setup_complete"),
                RichNotificationsEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "rich_notifications_eligible"),
                RingtonesEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "ringtones_enabled"),
                ShadowCorrectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "shadow_correction_enabled"),
                SheilaCameraEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "sheila_camera_eligible"),
                SheilaCameraProcessingEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "sheila_camera_processing_eligible"),
                Show24x7Lite = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_24x7_lite"),
                ShowOfflineMotionEvents = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_offline_motion_events"),
                ShowRadarData = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_radar_data"),
                VehicleDetectionEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "vehicle_detection_eligible")
            };

            var cv = new RingDeviceCvSettings
            {
                DetectionTypesVehicleAction = RingDeviceSettingsParser.Text(f, notes, "cv_settings", "detection_types", "vehicle", "action")
            };

            var general = new RingDeviceGeneralSettings
            {
                CountryCode = RingDeviceSettingsParser.Text(f, notes, "general_settings", "country_code"),
                CountryCodeEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "country_code_enabled"),
                DataCollectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "data_collection_enabled"),
                EnableAudioRecording = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "enable_audio_recording"),
                EventsViaRss = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "events_via_rss"),
                FirmwaresLocked = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "firmwares_locked"),
                Lite24x7Enabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "lite_24x7_enabled"),
                Lite24x7Subscribed = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "lite_24x7_subscribed"),
                OfflineMotionEventEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "offline_motion_event_enabled"),
                OfflineMotionEventSubscribed = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "offline_motion_event_subscribed"),
                TheftAlarmEnable = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "theft_alarm_enable"),
                UseWrapupDomain = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "use_wrapup_domain"),
                UtcOffset = RingDeviceSettingsParser.Text(f, notes, "general_settings", "utc_offset"),
                WrapupDomain = RingDeviceSettingsParser.Text(f, notes, "general_settings", "wrapup_domain")
            };

            var misc = new RingDeviceMiscSettings
            {
                AttestationRdaEnabled = RingDeviceSettingsParser.Bool(f, notes, "attestation_settings", "rda_enabled"),
                AuthFallbackEnabled = RingDeviceSettingsParser.Bool(f, notes, "auth_settings", "fallback_enabled"),
                AuthProtocol = RingDeviceSettingsParser.Text(f, notes, "auth_settings", "protocol"),
                AuthRetryInterval = RingDeviceSettingsParser.Int(f, notes, "auth_settings", "retry_interval"),
                EventOnDemandMaxDurationS = RingDeviceSettingsParser.Int(f, notes, "event_settings", "on_demand", "max_duration_s"),
                FloodlightAlwaysOnDuration = RingDeviceSettingsParser.Text(f, notes, "floodlight_settings", "always_on_duration"),
                KeepAliveAuto = RingDeviceSettingsParser.Int(f, notes, "keep_alive_settings", "keep_alive_auto"),
                LightSnoozeDuration = RingDeviceSettingsParser.Int(f, notes, "light_snooze_settings", "duration"),
                Lite24x7Mode = RingDeviceSettingsParser.Text(f, notes, "lite_24x7", "mode"),
                SheilaSessionRegistrationApi = RingDeviceSettingsParser.Int(f, notes, "sheila_settings", "session_registration_api"),
                Type = RingDeviceSettingsParser.Text(f, notes, "type"),
                VolumeMicVolume = RingDeviceSettingsParser.Int(f, notes, "volume_settings", "mic_volume"),
                VolumeVoiceVolume = RingDeviceSettingsParser.Int(f, notes, "volume_settings", "voice_volume")
            };

            var motion = new RingDeviceMotionSettings
            {
                AdvancedMotionDetectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_enabled"),
                AdvancedMotionDetectionHumanOnlyMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_human_only_mode"),
                AdvancedMotionDetectionLoiteringMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_loitering_mode"),
                AdvancedMotionDetectionMode = RingDeviceSettingsParser.Text(f, notes, "motion_settings", "advanced_motion_detection_mode"),
                AdvancedMotionGlanceEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_glance_enabled"),
                AdvancedMotionRecordingHumanMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_recording_human_mode"),
                AdvancedMotionZonesEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_zones_enabled"),
                AdvancedMotionZonesType = RingDeviceSettingsParser.Text(f, notes, "motion_settings", "advanced_motion_zones_type"),
                AdvancedPirMotionZonesZone1Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone1_sensitivity"),
                AdvancedPirMotionZonesZone2Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone2_sensitivity"),
                AdvancedPirMotionZonesZone3Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone3_sensitivity"),
                AdvancedPirMotionZonesZone4Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone4_sensitivity"),
                AdvancedPirMotionZonesZone5Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone5_sensitivity"),
                AdvancedPirMotionZonesZone6Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone6_sensitivity"),
                EnableAudio = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "enable_audio"),
                EnableIr = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "enable_ir"),
                EnableRecording = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "enable_recording"),
                EndDetection = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "end_detection"),
                LoiteringThreshold = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "loitering_threshold"),
                MotionSensitivityProfile = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "motion_sensitivity_profile"),
                MotionSnoozeDeviceEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "motion_snooze_device_enabled"),
                MotionSnoozeThresholdMins = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "motion_snooze_threshold_mins"),
                ZoneSettingsV2Enabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "zone_settings_v2_enabled")
            };

            var snapshot = new RingDeviceSnapshotSettings
            {
                CloseContainer = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "close_container"),
                FrequencyAfterSecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "frequency_after_secs"),
                FrequencySecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "frequency_secs"),
                Lite24x7ResolutionP = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "lite_24x7_resolution_p"),
                MaxUploadKb = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "max_upload_kb"),
                OmeResolutionP = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "ome_resolution_p"),
                PeriodAfterSecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "period_after_secs")
            };

            var stream = new RingDeviceStreamSettings
            {
                ActiveStreamingProfile = RingDeviceSettingsParser.Text(f, notes, "stream_settings", "active_streaming_profile"),
                Profile = RingDeviceSettingsParser.Int(f, notes, "stream_settings", "profile"),
                StreamingProfilesRmsHost = RingDeviceSettingsParser.Text(f, notes, "stream_settings", "streaming_profiles", "rms", "host"),
                StreamingProfilesRmsPort = RingDeviceSettingsParser.Int(f, notes, "stream_settings", "streaming_profiles", "rms", "port")
            };

            var video = new RingDeviceVideoSettings
            {
                AbcEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "abc_enabled"),
                AeLevel = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ae_level"),
                AeMode = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ae_mode"),
                AspectRatio = RingDeviceSettingsParser.Text(f, notes, "video_settings", "aspect_ratio"),
                AutoClipLengthEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "auto_clip_length_enabled"),
                Brightness = RingDeviceSettingsParser.Int(f, notes, "video_settings", "brightness"),
                ClipLengthMax = RingDeviceSettingsParser.Int(f, notes, "video_settings", "clip_length_max"),
                ClipLengthMin = RingDeviceSettingsParser.Int(f, notes, "video_settings", "clip_length_min"),
                Contrast = RingDeviceSettingsParser.Int(f, notes, "video_settings", "contrast"),
                DynamicResolutionEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "dynamic_resolution_enabled"),
                ExposureControl = RingDeviceSettingsParser.Int(f, notes, "video_settings", "exposure_control"),
                HdrEnable = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "hdr_enable"),
                IgnoreZonesZone1Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone1", "name"),
                IgnoreZonesZone1State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "state"),
                IgnoreZonesZone1Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex1", "x"),
                IgnoreZonesZone1Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex1", "y"),
                IgnoreZonesZone1Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex2", "x"),
                IgnoreZonesZone1Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex2", "y"),
                IgnoreZonesZone2Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone2", "name"),
                IgnoreZonesZone2State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "state"),
                IgnoreZonesZone2Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex1", "x"),
                IgnoreZonesZone2Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex1", "y"),
                IgnoreZonesZone2Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex2", "x"),
                IgnoreZonesZone2Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex2", "y"),
                IgnoreZonesZone3Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone3", "name"),
                IgnoreZonesZone3State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "state"),
                IgnoreZonesZone3Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex1", "x"),
                IgnoreZonesZone3Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex1", "y"),
                IgnoreZonesZone3Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex2", "x"),
                IgnoreZonesZone3Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex2", "y"),
                IgnoreZonesZone4Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone4", "name"),
                IgnoreZonesZone4State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "state"),
                IgnoreZonesZone4Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex1", "x"),
                IgnoreZonesZone4Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex1", "y"),
                IgnoreZonesZone4Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex2", "x"),
                IgnoreZonesZone4Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex2", "y"),
                NightColorEnable = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "night_color_enable"),
                RetinalTuningEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "retinal_tuning", "enabled"),
                RetinalTuningUserSpecifiedEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "retinal_tuning", "user_specified_enabled"),
                Saturation = RingDeviceSettingsParser.Int(f, notes, "video_settings", "saturation")
            };

            var vod = new RingDeviceVodSettings
            {
                CachedVodDomain = RingDeviceSettingsParser.Text(f, notes, "vod_settings", "cached_vod_domain"),
                Enable = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "enable"),
                ForcedEnableVod = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "forced_enable_vod"),
                ToggledAt = RingDeviceSettingsParser.Text(f, notes, "vod_settings", "toggled_at"),
                UseCachedVodDomain = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "use_cached_vod_domain")
            };

            List<RingDeviceMotionSnoozeProfile> snooze = ParseSnooze(f, notes);
            var versions = new List<RingDeviceCvThresholdOverrideVersion>();
            var values = new List<RingDeviceCvThresholdOverrideValue>();
            ParseThresholds(f, notes, versions, values);
            return new RingDeviceSettingGroupRows(advancedMotion, backend, clientDevice, cv, general, misc, motion, snapshot, stream, video, vod, snooze, versions, values);
        }

        private static List<RingDeviceMotionSnoozeProfile> ParseSnooze(IReadOnlyDictionary<string, JsonElement> f, List<string> notes)
        {
            var rows = new List<RingDeviceMotionSnoozeProfile>();
            if (ArrayAt(f, notes, new[] { "motion_settings", "motion_snooze_profile" }) is not { } items)
            {
                return rows;
            }

            int position = 0;
            foreach (JsonElement item in items.EnumerateArray())
            {
                rows.Add(new RingDeviceMotionSnoozeProfile { Position = position, Value = ElementInt(item, notes, "motion_settings.motion_snooze_profile[]") });
                position++;
            }

            return rows;
        }

        /// <summary>Reads cv_settings.threshold_overrides.package_delivery.versions: one version row per entry and one value row per number in its values.</summary>
        private static void ParseThresholds(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, List<RingDeviceCvThresholdOverrideVersion> versions, List<RingDeviceCvThresholdOverrideValue> values)
        {
            if (ArrayAt(f, notes, new[] { "cv_settings", "threshold_overrides", "package_delivery", "versions" }) is not { } items)
            {
                return;
            }

            int versionPosition = 0;
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    notes.Add($"cv_settings.threshold_overrides.package_delivery.versions[] not stored: expected an object ({item.ValueKind})");
                    versionPosition++;
                    continue;
                }

                versions.Add(new RingDeviceCvThresholdOverrideVersion
                {
                    Position = versionPosition,
                    Version = PropertyText(item, "version", notes, "cv_settings.threshold_overrides.package_delivery.versions[].version"),
                });
                if (item.TryGetProperty("values", out JsonElement elements) && elements.ValueKind == JsonValueKind.Array)
                {
                    int position = 0;
                    foreach (JsonElement element in elements.EnumerateArray())
                    {
                        values.Add(new RingDeviceCvThresholdOverrideValue
                        {
                            VersionPosition = versionPosition,
                            Position = position,
                            Value = ElementDouble(element, notes, "cv_settings.threshold_overrides.package_delivery.versions[].values[]"),
                        });
                        position++;
                    }
                }

                versionPosition++;
            }
        }

        /// <summary>The array at the path, or null when the key is absent or null. A value that is not an array is noted (key and kind only) and yields null.</summary>
        private static JsonElement? ArrayAt(IReadOnlyDictionary<string, JsonElement> f, List<string> notes, string[] path)
        {
            if (RingDeviceSettingsParser.Find(f, notes, path) is not { } e || e.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (e.ValueKind == JsonValueKind.Array)
            {
                return e;
            }

            notes.Add($"{string.Join('.', path)} not stored: expected an array ({e.ValueKind})");
            return null;
        }

        private static int? ElementInt(JsonElement e, List<string> notes, string key)
        {
            if (e.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            bool ok = RingJsonValue.TryInt(e, out int? r);
            if (!ok)
            {
                notes.Add($"{key} not stored: unexpected shape ({e.ValueKind})");
                return null;
            }

            return r;
        }

        private static double? ElementDouble(JsonElement e, List<string> notes, string key)
        {
            if (e.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            bool ok = RingJsonValue.TryDouble(e, out double? r);
            if (!ok)
            {
                notes.Add($"{key} not stored: unexpected shape ({e.ValueKind})");
                return null;
            }

            return r;
        }

        private static string? PropertyText(JsonElement obj, string name, List<string> notes, string key)
        {
            if (!obj.TryGetProperty(name, out JsonElement v) || v.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            bool ok = RingJsonValue.TryText(v, out string? r);
            if (!ok)
            {
                notes.Add($"{key} not stored: unexpected shape ({v.ValueKind})");
                return null;
            }

            return r;
        }
    }
}
