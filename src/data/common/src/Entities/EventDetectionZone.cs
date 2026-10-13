namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Motion or detection zone within an event detection.</summary>
    public class EventDetectionZone
    {
        public Guid Id { get; set; }
        public Guid EventDetectionId { get; set; }
        public required string ZoneId { get; set; }
        public string? ZoneName { get; set; } = null;
        public decimal? Confidence { get; set; }
    }
}
