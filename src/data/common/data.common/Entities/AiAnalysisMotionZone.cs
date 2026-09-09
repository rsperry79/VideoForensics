namespace VideoForensics.Data.Common.Entities
{
    /// <summary>AI analysis motion zone associated with an AI analysis snapshot.</summary>
    public class AiAnalysisMotionZone
    {
        /// <summary>Gets or sets the primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the AI analysis snapshot ID (foreign key).</summary>
        public Guid AiAnalysisSnapshotId { get; set; }

        /// <summary>Gets or sets the zone ID.</summary>
        public string ZoneId { get; set; } = string.Empty;

        /// <summary>Gets or sets the zone name.</summary>
        public string ZoneName { get; set; } = string.Empty;

        /// <summary>Gets or sets the confidence score for motion detection in this zone.</summary>
        public decimal? Confidence { get; set; }

        /// <summary>Gets or sets the navigation property to the AI analysis snapshot.</summary>
        public virtual AiAnalysisSnapshot? AiAnalysisSnapshot { get; set; }
    }
}
