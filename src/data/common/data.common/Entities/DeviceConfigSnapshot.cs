namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only snapshot of device configuration settings.</summary>
    public class DeviceConfigSnapshot
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public bool? MotionDetectionEnabled { get; set; }
        public string? MotionSensitivity { get; set; }
        public string? RecordingMode { get; set; }
        public string? CustomSettingsJson { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DeviceConfigSource Source { get; set; }
    }
}
