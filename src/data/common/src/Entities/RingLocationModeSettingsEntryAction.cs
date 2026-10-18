namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One action of a mode entry. Kind is enabled (applied in this mode, ActionGroup null) or
    /// supported (an action the device supports, ActionGroup holds the group key such as liveView).
    /// </summary>
    public class RingLocationModeSettingsEntryAction
    {
        public Guid Id { get; set; }
        public Guid RingLocationModeSettingsEntryId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string? ActionGroup { get; set; }
        public int Position { get; set; }
        public string ActionName { get; set; } = string.Empty;
    }
}
