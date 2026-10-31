namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.snapshot_settings object (capture frequency, periods and resolutions). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceSnapshotSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.snapshot_settings.close_container.</summary>
        public int? CloseContainer { get; set; }

        /// <summary>settings.snapshot_settings.frequency_after_secs.</summary>
        public int? FrequencyAfterSecs { get; set; }

        /// <summary>settings.snapshot_settings.frequency_secs.</summary>
        public int? FrequencySecs { get; set; }

        /// <summary>settings.snapshot_settings.lite_24x7_resolution_p.</summary>
        public int? Lite24x7ResolutionP { get; set; }

        /// <summary>settings.snapshot_settings.max_upload_kb.</summary>
        public int? MaxUploadKb { get; set; }

        /// <summary>settings.snapshot_settings.ome_resolution_p.</summary>
        public int? OmeResolutionP { get; set; }

        /// <summary>settings.snapshot_settings.period_after_secs.</summary>
        public int? PeriodAfterSecs { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
