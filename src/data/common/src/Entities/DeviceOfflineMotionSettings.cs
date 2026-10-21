namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Ring's offline_motion_event_settings for one device: upload and sampling behaviour for motion recorded while offline.</summary>
    public class DeviceOfflineMotionSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public bool? Subscribed { get; set; }
        public bool? Enabled { get; set; }
        public int? MaxUploadKb { get; set; }
        public int? ResolutionP { get; set; }
        public int? FrequencyAfterSecs { get; set; }
        public int? PeriodAfterSecs { get; set; }
    }
}
