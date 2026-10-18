namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One supported action group in a location mode settings options block.</summary>
    public class RingLocationModeSupportedActionGroup
    {
        public Guid Id { get; set; }
        public Guid RingLocationModeSettingsId { get; set; }
        public int Position { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }
}
