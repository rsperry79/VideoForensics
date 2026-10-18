namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One advanced object setting of a stickup cam (e.g. human_detection_confidence), with its day and night values.</summary>
    public class DeviceAdvancedObjectSetting
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Ring's key for the setting, e.g. "human_detection_confidence".</summary>
        public string SettingKey { get; set; } = string.Empty;
        public double? Day { get; set; }
        public double? Night { get; set; }
    }
}
