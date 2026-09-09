namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Device capability flags normalized from provider responses (P1 entity for provider metadata normalization).</summary>
    public class DeviceFeatures
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public bool? MotionsEnabled { get; set; }
        public bool? ShowRecordings { get; set; }
        public bool? AdvancedMotionEnabled { get; set; }
        public bool? PeopleOnlyEnabled { get; set; }
        public bool? ShadowCorrectionEnabled { get; set; }
        public bool? MotionMessageEnabled { get; set; }
        public bool? NightVisionEnabled { get; set; }
    }
}
