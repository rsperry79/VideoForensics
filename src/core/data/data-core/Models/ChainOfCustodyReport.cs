using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Report showing the complete audit trail for evidence (hash-chained action log).</summary>
    public class ChainOfCustodyReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<ActionLogEntry> AuditTrail { get; set; } = new List<ActionLogEntry>();
        public bool ChainIntegrityVerified { get; set; }
        public string? ChainVerificationStatus { get; set; }
    }
}
