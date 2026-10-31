namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of settings.cv_settings.threshold_overrides.package_delivery.versions, in the order Ring reports them.</summary>
    public class RingDeviceCvThresholdOverrideVersion
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Zero-based index in Ring's versions array; preserves order.</summary>
        public int Position { get; set; }

        /// <summary>The version label (versions[].version). Stored as Ring sends it.</summary>
        public string? Version { get; set; }
    }
}
