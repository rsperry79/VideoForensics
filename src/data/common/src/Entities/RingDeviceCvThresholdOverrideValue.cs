namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One value of settings.cv_settings.threshold_overrides.package_delivery.versions[].values, keyed by its version position.</summary>
    public class RingDeviceCvThresholdOverrideValue
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Zero-based index of the owning entry in versions; preserves order.</summary>
        public int VersionPosition { get; set; }

        /// <summary>Zero-based index inside that entry's values array; preserves order.</summary>
        public int Position { get; set; }

        /// <summary>The threshold value (a number).</summary>
        public double? Value { get; set; }
    }
}
