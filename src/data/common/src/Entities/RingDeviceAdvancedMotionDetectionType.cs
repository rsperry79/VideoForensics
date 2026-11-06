namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a device's settings.advanced_motion_detection_types array (for example "human"), in Ring's order.</summary>
    public class RingDeviceAdvancedMotionDetectionType
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Zero-based index in Ring's array.</summary>
        public int Position { get; set; }
        public string DetectionType { get; set; } = string.Empty;
    }
}
