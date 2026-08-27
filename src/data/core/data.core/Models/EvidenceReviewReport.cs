using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Report summarizing media items and their integrity verification status.</summary>
    public class EvidenceReviewReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
        public IReadOnlyList<IntegrityRecord> IntegrityRecords { get; set; } = new List<IntegrityRecord>();
        public int TotalItemCount { get; set; }
        public int VerifiedItemCount { get; set; }
        public int FailedVerificationCount { get; set; }
    }
}
