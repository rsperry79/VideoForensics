namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Aggregated jamming statistics for a single device, upserted from incident records.</summary>
    public class JammingStatsSummary
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public int IncidentCount { get; set; }
        public double TotalJammedDurationMinutes { get; set; }
        public double AverageDegradationDb { get; set; }
        public double MaxDegradationDb { get; set; }
        public int LowConfidenceCount { get; set; }
        public int MediumConfidenceCount { get; set; }
        public int HighConfidenceCount { get; set; }
        public int DefiniteConfidenceCount { get; set; }
        public DateTime? FirstIncidentUtc { get; set; }
        public DateTime? LastIncidentUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
