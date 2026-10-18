namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Location mode settings for a Ring location: options plus one entry per mode and device.</summary>
    public class RingLocationModeSettings
    {
        public Guid Id { get; set; }
        public string ProviderLocationId { get; set; } = string.Empty;
        public long? LastUpdateTimeMs { get; set; }
        public string? DefaultCameraOnlyMode { get; set; }
        public List<RingLocationModeSupportedActionGroup> SupportedActionGroups { get; set; } = [];
        public List<RingLocationModeSettingsEntry> Entries { get; set; } = [];
    }
}
