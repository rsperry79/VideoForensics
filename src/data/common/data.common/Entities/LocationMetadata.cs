namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Location metadata: address components, timezone, coordinates.</summary>
    public class LocationMetadata
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? TimeZoneId { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ApiResponseHash { get; set; }
        public string? MetadataJson { get; set; }
    }
}
