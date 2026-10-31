namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from a doorbot, stickup cam or chime <c>settings</c> object, plus the top-level <c>created_at</c>. 1:1 with the device,
    /// replaced on each sync. Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.stark_enabled.</summary>
        public bool? StarkEnabled { get; set; }

        /// <summary>settings.stark_enrolled.</summary>
        public bool? StarkEnrolled { get; set; }

        /// <summary>settings.enable_rich_notifications.</summary>
        public bool? EnableRichNotifications { get; set; }

        /// <summary>settings.rich_notifications_billing_eligible.</summary>
        public bool? RichNotificationsBillingEligible { get; set; }

        /// <summary>settings.rich_notifications_face_crop_enabled.</summary>
        public bool? RichNotificationsFaceCropEnabled { get; set; }

        /// <summary>settings.rich_notifications_scene_source. Stored as Ring sends it.</summary>
        public string? RichNotificationsSceneSource { get; set; }

        /// <summary>settings.people_detection_eligible.</summary>
        public bool? PeopleDetectionEligible { get; set; }

        /// <summary>
        /// settings.terms_of_service_accepted. Null on stickup cams in every captured sample, so the text type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public string? TermsOfServiceAccepted { get; set; }

        /// <summary>
        /// settings.motion_sensitivity_profile. Not present in any captured sample, so the integer type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public int? MotionSensitivityProfile { get; set; }

        /// <summary>settings.lite_24x7.enabled.</summary>
        public bool? Lite24x7Enabled { get; set; }

        /// <summary>settings.lite_24x7.frequency_secs.</summary>
        public int? Lite24x7FrequencySecs { get; set; }

        /// <summary>settings.lite_24x7.resolution_p.</summary>
        public int? Lite24x7ResolutionP { get; set; }

        /// <summary>settings.lite_24x7.subscribed.</summary>
        public bool? Lite24x7Subscribed { get; set; }

        /// <summary>settings.lite_24x7_footage_ttl.</summary>
        public int? Lite24x7FootageTtl { get; set; }

        /// <summary>settings.server_settings.ring_media_server_enabled.</summary>
        public bool? RingMediaServerEnabled { get; set; }

        /// <summary>settings.server_settings.ring_media_server_host. Stored as Ring sends it.</summary>
        public string? RingMediaServerHost { get; set; }

        /// <summary>settings.sheila_settings.cv_processing_enabled.</summary>
        public bool? SheilaCvProcessingEnabled { get; set; }

        /// <summary>settings.sheila_settings.local_storage_enabled.</summary>
        public bool? SheilaLocalStorageEnabled { get; set; }

        /// <summary>settings.video_settings.encryption_enabled.</summary>
        public bool? EncryptionEnabled { get; set; }

        /// <summary>settings.video_settings.encryption_method.</summary>
        public int? EncryptionMethod { get; set; }

        /// <summary>
        /// settings.video_settings.encryption_eligibility_mode. Null in every captured sample, so the text type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public string? EncryptionEligibilityMode { get; set; }

        /// <summary>settings.video_settings.recording_24x7_mode.</summary>
        public int? Recording24x7Mode { get; set; }

        /// <summary>settings.cv_settings.search_types.natural_language_search.enabled.</summary>
        public bool? NaturalLanguageSearchEnabled { get; set; }

        /// <summary>settings.cv_settings.search_types.natural_language_search.mode.</summary>
        public string? NaturalLanguageSearchMode { get; set; }

        /// <summary>settings.concierge_settings.mode.</summary>
        public string? ConciergeMode { get; set; }

        /// <summary>settings.concierge_settings.alexa_settings.delay_ms.</summary>
        public int? ConciergeAlexaDelayMs { get; set; }

        /// <summary>settings.concierge_settings.autoreply_settings.delay_ms.</summary>
        public int? ConciergeAutoreplyDelayMs { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone1_sensitivity.</summary>
        public int? PirZone1Sensitivity { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone2_sensitivity.</summary>
        public int? PirZone2Sensitivity { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone3_sensitivity.</summary>
        public int? PirZone3Sensitivity { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone4_sensitivity.</summary>
        public int? PirZone4Sensitivity { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone5_sensitivity.</summary>
        public int? PirZone5Sensitivity { get; set; }

        /// <summary>settings.advanced_pir_motion_zones.zone6_sensitivity.</summary>
        public int? PirZone6Sensitivity { get; set; }

        /// <summary>settings.audio_settings.enable_live_view_audio_override.</summary>
        public bool? EnableLiveViewAudioOverride { get; set; }

        /// <summary>
        /// settings.active_streaming_event_led_enabled. Null in every captured sample, so the boolean type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public bool? ActiveStreamingEventLedEnabled { get; set; }

        /// <summary>
        /// settings.flick_elim_recommended_mode. Null in every captured sample, so the text type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public string? FlickElimRecommendedMode { get; set; }

        /// <summary>
        /// Top-level created_at (UTC). Null when Ring did not send it or the text was not an ISO-8601 date-time (the value is not stored, only noted).
        /// </summary>
        public DateTime? RingCreatedAt { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
