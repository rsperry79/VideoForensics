namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Location mode sharing state for a Ring location.</summary>
    public class RingLocationModeSharing
    {
        public Guid Id { get; set; }
        public string ProviderLocationId { get; set; } = string.Empty;
        public bool? SharedUsersEnabled { get; set; }
        public long? LastUpdateTimeMs { get; set; }
    }
}
