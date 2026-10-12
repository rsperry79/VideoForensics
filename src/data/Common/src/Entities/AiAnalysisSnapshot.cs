namespace VideoForensics.Data.Common.Entities
{
    /// <summary>AI analysis snapshot captured during a download event.</summary>
    public class AiAnalysisSnapshot
    {
        public Guid Id { get; set; }
        public Guid DownloadEventId { get; set; }
        public bool? PersonDetected { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string? FullDescription { get; set; }

        /// <summary>Gets or sets the collection of tags for this analysis snapshot.</summary>
        public ICollection<AiAnalysisTag> Tags { get; set; } = [];

        /// <summary>Gets or sets the collection of motion zones for this analysis snapshot.</summary>
        public ICollection<AiAnalysisMotionZone> MotionZones { get; set; } = [];
    }
}
