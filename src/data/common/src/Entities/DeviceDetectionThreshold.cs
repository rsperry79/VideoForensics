namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One numeric cv_settings.threshold entry for a detection type (e.g. loitering = 10.0).</summary>
    public class DeviceDetectionThreshold
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string DetectionType { get; set; } = string.Empty;
        public double? Threshold { get; set; }
    }
}
