namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Individual detection occurrence with timestamp within an event detection.</summary>
    public class EventDetectionTypeOccurrence
    {
        public Guid Id { get; set; }
        public Guid EventDetectionId { get; set; }
        public required string DetectionType { get; set; }
        public DateTime DetectedAtUtc { get; set; }
    }
}
