namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Motion or detection zone within a media item detection.</summary>
    public class DetectionZone
    {
        public Guid Id { get; set; }
        public Guid MediaItemDetectionId { get; set; }
        public required string ZoneId { get; set; }
        public string? ZoneName { get; set; } = null;
        public decimal? Confidence { get; set; }
    }
}
