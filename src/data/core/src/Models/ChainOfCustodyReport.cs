using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Report showing the complete audit trail for evidence (hash-chained action log).</summary>
    public class ChainOfCustodyReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<ActionLogEntry> AuditTrail { get; set; } = [];
        public bool ChainIntegrityVerified { get; set; }
        public string? ChainVerificationStatus { get; set; }

        /// <summary>Entries that verified before the chain broke, or every entry when it is valid.</summary>
        public int ChainEntriesChecked { get; set; }

        /// <summary>The kind of break, or <see cref="ChainBreakKind.None"/> when the chain verifies.</summary>
        public ChainBreakKind ChainBreak { get; set; }

        /// <summary>The first entry that failed verification, or null when the chain verifies.</summary>
        public Guid? ChainFailedEntryId { get; set; }

        /// <summary>When the first failing entry was recorded, or null when the chain verifies.</summary>
        public DateTime? ChainFailedAtUtc { get; set; }
    }
}
