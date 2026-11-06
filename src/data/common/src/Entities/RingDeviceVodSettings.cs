namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.vod_settings object (VOD enablement and cached domain). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceVodSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.vod_settings.cached_vod_domain.</summary>
        public string? CachedVodDomain { get; set; }

        /// <summary>settings.vod_settings.enable.</summary>
        public bool? Enable { get; set; }

        /// <summary>settings.vod_settings.forced_enable_vod.</summary>
        public bool? ForcedEnableVod { get; set; }

        /// <summary>settings.vod_settings.toggled_at.</summary>
        public string? ToggledAt { get; set; }

        /// <summary>settings.vod_settings.use_cached_vod_domain.</summary>
        public bool? UseCachedVodDomain { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
