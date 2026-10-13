namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Ring CV detection data for a media item.</summary>
    public class MediaItemDetection
    {
        public Guid Id { get; set; }
        public Guid MediaItemId { get; set; }
        public bool? PersonDetected { get; set; }
        public bool? StreamBroken { get; set; }
        public string? DetectionType { get; set; } = null;
        public string? FullDescription { get; set; } = null;
        public string? ShortDescription { get; set; } = null;
        public decimal? Similarity { get; set; }
        public decimal? Anomaly { get; set; }
        public decimal? Confidence { get; set; }
        public string? ModelVersion { get; set; } = null;
    }
}
