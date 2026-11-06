namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Provider-agnostic scalar settings of a device: values from the <c>settings</c> object that any provider can send. 1:1 with the device, replaced on
    /// each sync. Every column is null when the provider did not send the key.
    /// </summary>
    public class DeviceGeneralSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.voice_volume.</summary>
        public int? VoiceVolume { get; set; }

        /// <summary>settings.live_view_disabled.</summary>
        public bool? LiveViewDisabled { get; set; }

        /// <summary>settings.motion_announcement.</summary>
        public bool? MotionAnnouncement { get; set; }

        /// <summary>settings.loitering_threshold.</summary>
        public int? LoiteringThreshold { get; set; }

        /// <summary>settings.user_specified_recording_ttl.</summary>
        public int? UserSpecifiedRecordingTtl { get; set; }

        /// <summary>settings.extended_live_view.</summary>
        public int? ExtendedLiveView { get; set; }

        /// <summary>settings.status_led_enable (chime settings).</summary>
        public bool? StatusLedEnabled { get; set; }

        /// <summary>settings.floodlight_settings.brightness.</summary>
        public int? FloodlightBrightness { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
