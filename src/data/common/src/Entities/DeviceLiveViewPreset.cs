namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a device's live view presets, in the order Ring reports them.</summary>
    public class DeviceLiveViewPreset
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Zero-based index in Ring's array; preserves order.</summary>
        public int Position { get; set; }
        public string Preset { get; set; } = string.Empty;
    }
}
