namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.general_settings object (country, data collection, offline motion and Ring domain flags). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceGeneralSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.general_settings.country_code.</summary>
        public string? CountryCode { get; set; }

        /// <summary>settings.general_settings.country_code_enabled.</summary>
        public bool? CountryCodeEnabled { get; set; }

        /// <summary>settings.general_settings.data_collection_enabled.</summary>
        public bool? DataCollectionEnabled { get; set; }

        /// <summary>settings.general_settings.enable_audio_recording.</summary>
        public bool? EnableAudioRecording { get; set; }

        /// <summary>settings.general_settings.events_via_rss.</summary>
        public bool? EventsViaRss { get; set; }

        /// <summary>settings.general_settings.firmwares_locked.</summary>
        public bool? FirmwaresLocked { get; set; }

        /// <summary>settings.general_settings.lite_24x7_enabled.</summary>
        public bool? Lite24x7Enabled { get; set; }

        /// <summary>settings.general_settings.lite_24x7_subscribed.</summary>
        public bool? Lite24x7Subscribed { get; set; }

        /// <summary>settings.general_settings.offline_motion_event_enabled.</summary>
        public bool? OfflineMotionEventEnabled { get; set; }

        /// <summary>settings.general_settings.offline_motion_event_subscribed.</summary>
        public bool? OfflineMotionEventSubscribed { get; set; }

        /// <summary>settings.general_settings.theft_alarm_enable.</summary>
        public bool? TheftAlarmEnable { get; set; }

        /// <summary>settings.general_settings.use_wrapup_domain.</summary>
        public bool? UseWrapupDomain { get; set; }

        /// <summary>settings.general_settings.utc_offset.</summary>
        public string? UtcOffset { get; set; }

        /// <summary>settings.general_settings.wrapup_domain.</summary>
        public string? WrapupDomain { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
