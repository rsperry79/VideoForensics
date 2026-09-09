namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Face recognition detection for a person in an event.</summary>
    public class EventDetectedPerson
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public required string ProfileId { get; set; }
        public string? ProfileName { get; set; } = null;
        public decimal? Confidence { get; set; }
        public string? ThumbnailUrl { get; set; } = null;
    }
}
