namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One device entry under one mode (disarmed, home or away).</summary>
    public class RingLocationModeSettingsEntry
    {
        public Guid Id { get; set; }
        public Guid RingLocationModeSettingsId { get; set; }
        public string Mode { get; set; } = string.Empty;
        public int Position { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceIdType { get; set; }
        public List<RingLocationModeSettingsEntryAction> Actions { get; set; } = [];
    }
}
