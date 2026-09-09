namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Individual detection occurrence with timestamp within a media item detection.</summary>
    public class DetectionTypeOccurrence
    {
        public Guid Id { get; set; }
        public Guid MediaItemDetectionId { get; set; }
        public required string DetectionType { get; set; }
        public DateTime DetectedAtUtc { get; set; }
    }
}
