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
            var advancedMotion = new RingDeviceAdvancedMotionSettings();
            advancedMotion.Zone1Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_1", "name");
            advancedMotion.Zone1State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "state");
            advancedMotion.Zone1Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex1", "x");
            advancedMotion.Zone1Vertex1Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex1", "y");
            advancedMotion.Zone1Vertex2X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex2", "x");
            advancedMotion.Zone1Vertex2Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex2", "y");
            advancedMotion.Zone1Vertex3X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex3", "x");
            advancedMotion.Zone1Vertex3Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex3", "y");
            advancedMotion.Zone1Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex4", "x");
            advancedMotion.Zone1Vertex4Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex4", "y");
            advancedMotion.Zone1Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex5", "x");
            advancedMotion.Zone1Vertex5Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex5", "y");
            advancedMotion.Zone1Vertex6X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex6", "x");
            advancedMotion.Zone1Vertex6Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex6", "y");
            advancedMotion.Zone1Vertex7X = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex7", "x");
            advancedMotion.Zone1Vertex7Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex7", "y");
            advancedMotion.Zone1Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_1", "vertex8", "x");
            advancedMotion.Zone1Vertex8Y = RingDeviceSettingsParser.Dbl(f, notes, "advanced_motion_settings", "zone_1", "vertex8", "y");
            advancedMotion.Zone2Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_2", "name");
            advancedMotion.Zone2State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "state");
            advancedMotion.Zone2Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex1", "x");
            advancedMotion.Zone2Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex1", "y");
            advancedMotion.Zone2Vertex2X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex2", "x");
            advancedMotion.Zone2Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex2", "y");
            advancedMotion.Zone2Vertex3X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex3", "x");
            advancedMotion.Zone2Vertex3Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex3", "y");
            advancedMotion.Zone2Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex4", "x");
            advancedMotion.Zone2Vertex4Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex4", "y");
            advancedMotion.Zone2Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex5", "x");
            advancedMotion.Zone2Vertex5Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex5", "y");
            advancedMotion.Zone2Vertex6X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex6", "x");
            advancedMotion.Zone2Vertex6Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex6", "y");
            advancedMotion.Zone2Vertex7X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex7", "x");
            advancedMotion.Zone2Vertex7Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex7", "y");
            advancedMotion.Zone2Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex8", "x");
            advancedMotion.Zone2Vertex8Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_2", "vertex8", "y");
            advancedMotion.Zone3Name = RingDeviceSettingsParser.Text(f, notes, "advanced_motion_settings", "zone_3", "name");
            advancedMotion.Zone3State = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "state");
            advancedMotion.Zone3Vertex1X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex1", "x");
            advancedMotion.Zone3Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex1", "y");
            advancedMotion.Zone3Vertex2X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex2", "x");
            advancedMotion.Zone3Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex2", "y");
            advancedMotion.Zone3Vertex3X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex3", "x");
            advancedMotion.Zone3Vertex3Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex3", "y");
            advancedMotion.Zone3Vertex4X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex4", "x");
            advancedMotion.Zone3Vertex4Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex4", "y");
            advancedMotion.Zone3Vertex5X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex5", "x");
            advancedMotion.Zone3Vertex5Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex5", "y");
            advancedMotion.Zone3Vertex6X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex6", "x");
            advancedMotion.Zone3Vertex6Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex6", "y");
            advancedMotion.Zone3Vertex7X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex7", "x");
            advancedMotion.Zone3Vertex7Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex7", "y");
            advancedMotion.Zone3Vertex8X = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex8", "x");
            advancedMotion.Zone3Vertex8Y = RingDeviceSettingsParser.Int(f, notes, "advanced_motion_settings", "zone_3", "vertex8", "y");

            var backend = new RingDeviceBackendSettings();
            backend.EnableRichNotifications = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "enable_rich_notifications");
            backend.LiveViewDisabled = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "live_view_disabled");
            backend.LiveViewPresetProfile = RingDeviceSettingsParser.Text(f, notes, "backend_settings", "live_view_preset_profile");
            backend.MotionSnoozePresetProfile = RingDeviceSettingsParser.Text(f, notes, "backend_settings", "motion_snooze_preset_profile");
            backend.MotionWarningEnabled = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "motion_warning_enabled");
            backend.PaidFeaturesAiAlexaPlusGreetings = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_alexa_plus_greetings");
            backend.PaidFeaturesAiAutomatedWarnings = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_automated_warnings");
            backend.PaidFeaturesAiLabsDailyClips = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_labs_daily_clips");
            backend.PaidFeaturesAiLabsMemorableMoments = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ai_labs_memorable_moments");
            backend.PaidFeaturesAlarmTriggeredRecording = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "alarm_triggered_recording");
            backend.PaidFeaturesAlexaConcierge = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "alexa_concierge");
            backend.PaidFeaturesAnimal = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "animal");
            backend.PaidFeaturesAppstore = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "appstore");
            backend.PaidFeaturesBabyCry = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "baby_cry");
            backend.PaidFeaturesCarAlarm = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "car_alarm");
            backend.PaidFeaturesClosedCaption = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "closed_caption");
            backend.PaidFeaturesCo2SmokeAlarm = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "co2_smoke_alarm");
            backend.PaidFeaturesCriticalAlerts = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "critical_alerts");
            backend.PaidFeaturesCvTriggers = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "cv_triggers");
            backend.PaidFeaturesDailyDigest = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "daily_digest");
            backend.PaidFeaturesDingCall = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "ding_call");
            backend.PaidFeaturesDogBark = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "dog_bark");
            backend.PaidFeaturesExtendedLiveView = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "extended_live_view");
            backend.PaidFeaturesGeneralSound = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "general_sound");
            backend.PaidFeaturesGlassBreak = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "glass_break");
            backend.PaidFeaturesHuman = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "human");
            backend.PaidFeaturesLiveSpeak = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "live_speak");
            backend.PaidFeaturesLoitering = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "loitering");
            backend.PaidFeaturesMotion = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "motion");
            backend.PaidFeaturesMotionAwareness = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "motion_awareness");
            backend.PaidFeaturesMotionStop = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "motion_stop");
            backend.PaidFeaturesMulticamLiveView = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "multicam_live_view");
            backend.PaidFeaturesNaturalLanguageSearch = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "natural_language_search");
            backend.PaidFeaturesOtherMotion = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "other_motion");
            backend.PaidFeaturesPackageDelivery = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "package_delivery");
            backend.PaidFeaturesPackagePickup = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "package_pickup");
            backend.PaidFeaturesPackageProtection = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "package_protection");
            backend.PaidFeaturesPersonIdentification = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "person_identification");
            backend.PaidFeaturesRecording24x7 = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "recording_24x7");
            backend.PaidFeaturesRetinalTuning = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "retinal_tuning");
            backend.PaidFeaturesRogersVqa = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "rogers_vqa");
            backend.PaidFeaturesSheilaCv = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "sheila_cv");
            backend.PaidFeaturesSheilaRecording = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "sheila_recording");
            backend.PaidFeaturesSmartMediaDescriptions = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "smart_media_descriptions");
            backend.PaidFeaturesSnapshotCapturePlus = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "snapshot_capture_plus");
            backend.PaidFeaturesSystemLevelPip = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "system_level_pip");
            backend.PaidFeaturesUnverifiedMotion = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "unverified_motion");
            backend.PaidFeaturesVehicle = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "paid_features", "vehicle");
            backend.TermsOfServiceAcceptedAutoreply = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "terms_of_service_accepted", "autoreply");
            backend.TermsOfServiceAcceptedConcierge = RingDeviceSettingsParser.Bool(f, notes, "backend_settings", "terms_of_service_accepted", "concierge");
            backend.UserSpecifiedFootageHoursTtl = RingDeviceSettingsParser.Int(f, notes, "backend_settings", "user_specified_footage_hours_ttl");
            backend.UserSpecifiedRecordingTtl = RingDeviceSettingsParser.Int(f, notes, "backend_settings", "user_specified_recording_ttl");

            var clientDevice = new RingDeviceClientDeviceSettings();
            clientDevice.AdvancedMotionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "advanced_motion_enabled");
            clientDevice.CfesEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "cfes_eligible");
            clientDevice.DynamicNetworkSwitchingEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "dynamic_network_switching_eligible");
            clientDevice.LightScheduleEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "light_schedule_enabled");
            clientDevice.LocalPlaybackEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "local_playback_enabled");
            clientDevice.MotionMessageEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "motion_message_enabled");
            clientDevice.MotionZoneRecommendation = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "motion_zone_recommendation");
            clientDevice.NightVisionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "night_vision_enabled");
            clientDevice.PeopleOnlyEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "people_only_enabled");
            clientDevice.PtzSetupComplete = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "ptz_setup_complete");
            clientDevice.RichNotificationsEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "rich_notifications_eligible");
            clientDevice.RingtonesEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "ringtones_enabled");
            clientDevice.ShadowCorrectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "shadow_correction_enabled");
            clientDevice.SheilaCameraEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "sheila_camera_eligible");
            clientDevice.SheilaCameraProcessingEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "sheila_camera_processing_eligible");
            clientDevice.Show24x7Lite = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_24x7_lite");
            clientDevice.ShowOfflineMotionEvents = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_offline_motion_events");
            clientDevice.ShowRadarData = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "show_radar_data");
            clientDevice.VehicleDetectionEligible = RingDeviceSettingsParser.Bool(f, notes, "client_device_settings", "vehicle_detection_eligible");

            var cv = new RingDeviceCvSettings();
            cv.DetectionTypesVehicleAction = RingDeviceSettingsParser.Text(f, notes, "cv_settings", "detection_types", "vehicle", "action");

            var general = new RingDeviceGeneralSettings();
            general.CountryCode = RingDeviceSettingsParser.Text(f, notes, "general_settings", "country_code");
            general.CountryCodeEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "country_code_enabled");
            general.DataCollectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "data_collection_enabled");
            general.EnableAudioRecording = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "enable_audio_recording");
            general.EventsViaRss = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "events_via_rss");
            general.FirmwaresLocked = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "firmwares_locked");
            general.Lite24x7Enabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "lite_24x7_enabled");
            general.Lite24x7Subscribed = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "lite_24x7_subscribed");
            general.OfflineMotionEventEnabled = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "offline_motion_event_enabled");
            general.OfflineMotionEventSubscribed = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "offline_motion_event_subscribed");
            general.TheftAlarmEnable = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "theft_alarm_enable");
            general.UseWrapupDomain = RingDeviceSettingsParser.Bool(f, notes, "general_settings", "use_wrapup_domain");
            general.UtcOffset = RingDeviceSettingsParser.Text(f, notes, "general_settings", "utc_offset");
            general.WrapupDomain = RingDeviceSettingsParser.Text(f, notes, "general_settings", "wrapup_domain");

            var misc = new RingDeviceMiscSettings();
            misc.AttestationRdaEnabled = RingDeviceSettingsParser.Bool(f, notes, "attestation_settings", "rda_enabled");
            misc.AuthFallbackEnabled = RingDeviceSettingsParser.Bool(f, notes, "auth_settings", "fallback_enabled");
            misc.AuthProtocol = RingDeviceSettingsParser.Text(f, notes, "auth_settings", "protocol");
            misc.AuthRetryInterval = RingDeviceSettingsParser.Int(f, notes, "auth_settings", "retry_interval");
            misc.EventOnDemandMaxDurationS = RingDeviceSettingsParser.Int(f, notes, "event_settings", "on_demand", "max_duration_s");
            misc.FloodlightAlwaysOnDuration = RingDeviceSettingsParser.Text(f, notes, "floodlight_settings", "always_on_duration");
            misc.KeepAliveAuto = RingDeviceSettingsParser.Int(f, notes, "keep_alive_settings", "keep_alive_auto");
            misc.LightSnoozeDuration = RingDeviceSettingsParser.Int(f, notes, "light_snooze_settings", "duration");
            misc.Lite24x7Mode = RingDeviceSettingsParser.Text(f, notes, "lite_24x7", "mode");
            misc.SheilaSessionRegistrationApi = RingDeviceSettingsParser.Int(f, notes, "sheila_settings", "session_registration_api");
            misc.Type = RingDeviceSettingsParser.Text(f, notes, "type");
            misc.VolumeMicVolume = RingDeviceSettingsParser.Int(f, notes, "volume_settings", "mic_volume");
            misc.VolumeVoiceVolume = RingDeviceSettingsParser.Int(f, notes, "volume_settings", "voice_volume");

            var motion = new RingDeviceMotionSettings();
            motion.AdvancedMotionDetectionEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_enabled");
            motion.AdvancedMotionDetectionHumanOnlyMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_human_only_mode");
            motion.AdvancedMotionDetectionLoiteringMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_detection_loitering_mode");
            motion.AdvancedMotionDetectionMode = RingDeviceSettingsParser.Text(f, notes, "motion_settings", "advanced_motion_detection_mode");
            motion.AdvancedMotionGlanceEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_glance_enabled");
            motion.AdvancedMotionRecordingHumanMode = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_recording_human_mode");
            motion.AdvancedMotionZonesEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "advanced_motion_zones_enabled");
            motion.AdvancedMotionZonesType = RingDeviceSettingsParser.Text(f, notes, "motion_settings", "advanced_motion_zones_type");
            motion.AdvancedPirMotionZonesZone1Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone1_sensitivity");
            motion.AdvancedPirMotionZonesZone2Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone2_sensitivity");
            motion.AdvancedPirMotionZonesZone3Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone3_sensitivity");
            motion.AdvancedPirMotionZonesZone4Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone4_sensitivity");
            motion.AdvancedPirMotionZonesZone5Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone5_sensitivity");
            motion.AdvancedPirMotionZonesZone6Sensitivity = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "advanced_pir_motion_zones", "zone6_sensitivity");
            motion.EnableAudio = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "enable_audio");
            motion.EnableIr = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "enable_ir");
            motion.EnableRecording = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "enable_recording");
            motion.EndDetection = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "end_detection");
            motion.LoiteringThreshold = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "loitering_threshold");
            motion.MotionSensitivityProfile = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "motion_sensitivity_profile");
            motion.MotionSnoozeDeviceEnabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "motion_snooze_device_enabled");
            motion.MotionSnoozeThresholdMins = RingDeviceSettingsParser.Int(f, notes, "motion_settings", "motion_snooze_threshold_mins");
            motion.ZoneSettingsV2Enabled = RingDeviceSettingsParser.Bool(f, notes, "motion_settings", "zone_settings_v2_enabled");

            var snapshot = new RingDeviceSnapshotSettings();
            snapshot.CloseContainer = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "close_container");
            snapshot.FrequencyAfterSecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "frequency_after_secs");
            snapshot.FrequencySecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "frequency_secs");
            snapshot.Lite24x7ResolutionP = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "lite_24x7_resolution_p");
            snapshot.MaxUploadKb = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "max_upload_kb");
            snapshot.OmeResolutionP = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "ome_resolution_p");
            snapshot.PeriodAfterSecs = RingDeviceSettingsParser.Int(f, notes, "snapshot_settings", "period_after_secs");

            var stream = new RingDeviceStreamSettings();
            stream.ActiveStreamingProfile = RingDeviceSettingsParser.Text(f, notes, "stream_settings", "active_streaming_profile");
            stream.Profile = RingDeviceSettingsParser.Int(f, notes, "stream_settings", "profile");
            stream.StreamingProfilesRmsHost = RingDeviceSettingsParser.Text(f, notes, "stream_settings", "streaming_profiles", "rms", "host");
            stream.StreamingProfilesRmsPort = RingDeviceSettingsParser.Int(f, notes, "stream_settings", "streaming_profiles", "rms", "port");

            var video = new RingDeviceVideoSettings();
            video.AbcEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "abc_enabled");
            video.AeLevel = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ae_level");
            video.AeMode = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ae_mode");
            video.AspectRatio = RingDeviceSettingsParser.Text(f, notes, "video_settings", "aspect_ratio");
            video.AutoClipLengthEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "auto_clip_length_enabled");
            video.Brightness = RingDeviceSettingsParser.Int(f, notes, "video_settings", "brightness");
            video.ClipLengthMax = RingDeviceSettingsParser.Int(f, notes, "video_settings", "clip_length_max");
            video.ClipLengthMin = RingDeviceSettingsParser.Int(f, notes, "video_settings", "clip_length_min");
            video.Contrast = RingDeviceSettingsParser.Int(f, notes, "video_settings", "contrast");
            video.DynamicResolutionEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "dynamic_resolution_enabled");
            video.ExposureControl = RingDeviceSettingsParser.Int(f, notes, "video_settings", "exposure_control");
            video.HdrEnable = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "hdr_enable");
            video.IgnoreZonesZone1Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone1", "name");
            video.IgnoreZonesZone1State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "state");
            video.IgnoreZonesZone1Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex1", "x");
            video.IgnoreZonesZone1Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex1", "y");
            video.IgnoreZonesZone1Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex2", "x");
            video.IgnoreZonesZone1Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone1", "vertex2", "y");
            video.IgnoreZonesZone2Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone2", "name");
            video.IgnoreZonesZone2State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "state");
            video.IgnoreZonesZone2Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex1", "x");
            video.IgnoreZonesZone2Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex1", "y");
            video.IgnoreZonesZone2Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex2", "x");
            video.IgnoreZonesZone2Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone2", "vertex2", "y");
            video.IgnoreZonesZone3Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone3", "name");
            video.IgnoreZonesZone3State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "state");
            video.IgnoreZonesZone3Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex1", "x");
            video.IgnoreZonesZone3Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex1", "y");
            video.IgnoreZonesZone3Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex2", "x");
            video.IgnoreZonesZone3Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone3", "vertex2", "y");
            video.IgnoreZonesZone4Name = RingDeviceSettingsParser.Text(f, notes, "video_settings", "ignore_zones", "zone4", "name");
            video.IgnoreZonesZone4State = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "state");
            video.IgnoreZonesZone4Vertex1X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex1", "x");
            video.IgnoreZonesZone4Vertex1Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex1", "y");
            video.IgnoreZonesZone4Vertex2X = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex2", "x");
            video.IgnoreZonesZone4Vertex2Y = RingDeviceSettingsParser.Int(f, notes, "video_settings", "ignore_zones", "zone4", "vertex2", "y");
            video.NightColorEnable = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "night_color_enable");
            video.RetinalTuningEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "retinal_tuning", "enabled");
            video.RetinalTuningUserSpecifiedEnabled = RingDeviceSettingsParser.Bool(f, notes, "video_settings", "retinal_tuning", "user_specified_enabled");
            video.Saturation = RingDeviceSettingsParser.Int(f, notes, "video_settings", "saturation");

            var vod = new RingDeviceVodSettings();
            vod.CachedVodDomain = RingDeviceSettingsParser.Text(f, notes, "vod_settings", "cached_vod_domain");
            vod.Enable = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "enable");
            vod.ForcedEnableVod = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "forced_enable_vod");
            vod.ToggledAt = RingDeviceSettingsParser.Text(f, notes, "vod_settings", "toggled_at");
            vod.UseCachedVodDomain = RingDeviceSettingsParser.Bool(f, notes, "vod_settings", "use_cached_vod_domain");

            var snooze = ParseSnooze(f, notes);
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
