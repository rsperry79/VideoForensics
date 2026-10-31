namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings groups with three or fewer keys (attestation, auth, event, floodlight, keep-alive, light-snooze, lite 24x7, sheila, volume) and the top-level type; each column is prefixed with its group. 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceMiscSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.attestation_settings.rda_enabled.</summary>
        public bool? AttestationRdaEnabled { get; set; }

        /// <summary>settings.auth_settings.fallback_enabled.</summary>
        public bool? AuthFallbackEnabled { get; set; }

        /// <summary>settings.auth_settings.protocol.</summary>
        public string? AuthProtocol { get; set; }

        /// <summary>settings.auth_settings.retry_interval.</summary>
        public int? AuthRetryInterval { get; set; }

        /// <summary>settings.event_settings.on_demand.max_duration_s.</summary>
        public int? EventOnDemandMaxDurationS { get; set; }

        /// <summary>settings.floodlight_settings.always_on_duration.</summary>
        public string? FloodlightAlwaysOnDuration { get; set; }

        /// <summary>settings.keep_alive_settings.keep_alive_auto.</summary>
        public int? KeepAliveAuto { get; set; }

        /// <summary>settings.light_snooze_settings.duration.</summary>
        public int? LightSnoozeDuration { get; set; }

        /// <summary>settings.lite_24x7.mode.</summary>
        public string? Lite24x7Mode { get; set; }

        /// <summary>settings.sheila_settings.session_registration_api.</summary>
        public int? SheilaSessionRegistrationApi { get; set; }

        /// <summary>settings.type.</summary>
        public string? Type { get; set; }

        /// <summary>settings.volume_settings.mic_volume.</summary>
        public int? VolumeMicVolume { get; set; }

        /// <summary>settings.volume_settings.voice_volume.</summary>
        public int? VolumeVoiceVolume { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
