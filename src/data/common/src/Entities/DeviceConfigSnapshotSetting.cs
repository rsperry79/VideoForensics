namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One vendor-specific setting stored on a device configuration snapshot.</summary>
    public class DeviceConfigSnapshotSetting
    {
        public Guid Id { get; set; }
        public Guid SnapshotId { get; set; }
        public string Key { get; set; } = string.Empty;

        /// <summary>The setting's value as JSON text (for example <c>1</c>, <c>"b"</c> or an object).</summary>
        public string ValueJson { get; set; } = string.Empty;
    }
}
