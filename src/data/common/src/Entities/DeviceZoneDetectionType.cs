namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a zone's properties.detection_types, in Ring order.</summary>
    public class DeviceZoneDetectionType
    {
        public Guid Id { get; set; }
        public Guid DeviceZoneId { get; set; }
        /// <summary>Zero-based index in Ring's array; preserves order.</summary>
        public int Position { get; set; }
        public string DetectionType { get; set; } = string.Empty;
    }
}
