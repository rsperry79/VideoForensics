namespace VideoForensics.Data.Common.Entities
{
    /// <summary>AI analysis tag associated with an AI analysis snapshot.</summary>
    public class AiAnalysisTag
    {
        /// <summary>Gets or sets the primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the AI analysis snapshot ID (foreign key).</summary>
        public Guid AiAnalysisSnapshotId { get; set; }

        /// <summary>Gets or sets the tag name.</summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>Gets or sets the navigation property to the AI analysis snapshot.</summary>
        public virtual AiAnalysisSnapshot? AiAnalysisSnapshot { get; set; }
    }
}
