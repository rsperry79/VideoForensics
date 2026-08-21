using System;

namespace Ring.Api.Forensics.Models
{
    public class EvidenceRetentionInfo
    {
        public string EvidenceId { get; set; } = string.Empty;
        public DateTime RetentionStartDate { get; set; } = DateTime.UtcNow;
        public DateTime RetentionEndDate { get; set; }
        public TimeSpan RetentionPeriod { get; set; }
        public RetentionStatus Status { get; set; }
        public string? RetentionReason { get; set; }
        public string? LegalReference { get; set; }
        public string? CaseNumber { get; set; }
        public DateTime? ApprovedForDestructionAt { get; set; }
        public string? DestructionAuthorizedBy { get; set; }
        public DateTime? DestructedAt { get; set; }
        public string? DestructionMethod { get; set; }
        public string? DestructionVerificationHash { get; set; }
        public bool IsExtendable { get; set; } = true;
        public DateTime? LastExtendedAt { get; set; }
    }

    public enum RetentionStatus
    {
        Active,
        PendingApprovalForDestruction,
        ApprovedForDestruction,
        Destroyed,
        OnHold
    }
}
