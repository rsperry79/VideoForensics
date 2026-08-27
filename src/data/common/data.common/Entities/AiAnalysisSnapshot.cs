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
        public string? TagsJson { get; set; }
        public string? MotionZonesJson { get; set; }
    }
}
