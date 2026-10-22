namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One CV detection type from cv_settings.detection_types (e.g. "human") and its mode and output flags.</summary>
    public class DeviceDetectionType
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Ring's detection type key, e.g. "human", "package_delivery".</summary>
        public string DetectionType { get; set; } = string.Empty;
        public bool? Enabled { get; set; }
        public string? Mode { get; set; }
        public bool? Record { get; set; }
        public bool? Notification { get; set; }
    }
}
