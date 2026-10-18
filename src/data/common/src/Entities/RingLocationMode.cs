namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Current home, away or disarmed mode of a Ring location (rs/mode/location/{id}).</summary>
    public class RingLocationMode
    {
        public Guid Id { get; set; }
        public string ProviderLocationId { get; set; } = string.Empty;
        public string? Mode { get; set; }
        public long? LastUpdateTimeMs { get; set; }
        public string? SecurityStatusMode { get; set; }
        public long? SecurityStatusLastUpdateMs { get; set; }
        public bool? ReadOnly { get; set; }
        public string? ModePanelType { get; set; }
        public List<RingLocationModeNotYetParticipating> NotYetParticipating { get; set; } = [];
    }
}
