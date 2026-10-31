namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.stream_settings object (active profile and the RMS host and port). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceStreamSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.stream_settings.active_streaming_profile.</summary>
        public string? ActiveStreamingProfile { get; set; }

        /// <summary>settings.stream_settings.profile.</summary>
        public int? Profile { get; set; }

        /// <summary>settings.stream_settings.streaming_profiles.rms.host.</summary>
        public string? StreamingProfilesRmsHost { get; set; }

        /// <summary>settings.stream_settings.streaming_profiles.rms.port.</summary>
        public int? StreamingProfilesRmsPort { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
