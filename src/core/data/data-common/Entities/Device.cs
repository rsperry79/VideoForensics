namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A device (camera, doorbell, etc.) linked to a location.</summary>
    public class Device
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public required string ProviderDeviceId { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public bool IsOnline { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime? LastSuccessfulPullAtUtc { get; set; }
        public DateTime? LastPullAttemptAtUtc { get; set; }
        public string? TimeZoneId { get; set; }
    }
}
