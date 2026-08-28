namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An event detected by a device, independent of whether it was downloaded.</summary>
    public class Event
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public required string ProviderEventId { get; set; }
        public required string EventType { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? SnapshotUrl { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime DiscoveredAtUtc { get; set; }
        public DateTime? DownloadedAtUtc { get; set; }
        public string? ApiSourceHash { get; set; }
        public string? EventIntegrityHash { get; set; }
    }
}
