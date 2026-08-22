using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Forensics.Models;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Manages evidence retention policies and destruction authorization.
    /// DV cases may require extended retention for re-prosecution or new charges.
    /// </summary>
    public interface IEvidenceRetentionPolicy
    {
        /// <summary>
        /// Set retention period for evidence based on case requirements.
        /// </summary>
        Task SetRetentionPeriodAsync(string evidenceId, TimeSpan retentionPeriod, string reason, string authorizedBy);

        /// <summary>
        /// Get current retention status for evidence.
        /// </summary>
        Task<EvidenceRetentionInfo> GetRetentionStatusAsync(string evidenceId);

        /// <summary>
        /// Check if evidence can be destroyed based on legal requirements.
        /// </summary>
        Task<bool> CanDestroyEvidenceAsync(string evidenceId);

        /// <summary>
        /// Request approval for evidence destruction.
        /// </summary>
        Task RequestDestructionApprovalAsync(string evidenceId, string requestedBy, string reason);

        /// <summary>
        /// Approve destruction of evidence (requires authorization).
        /// </summary>
        Task ApproveDestructionAsync(string evidenceId, string approvedBy, string method);

        /// <summary>
        /// Destroy evidence and record destruction with verification.
        /// </summary>
        Task DestroyEvidenceAsync(string evidenceId, string destructionMethod, string verificationHash);

        /// <summary>
        /// Extend retention period if case is ongoing or new charges emerge.
        /// </summary>
        Task ExtendRetentionAsync(string evidenceId, TimeSpan additionalPeriod, string reason, string authorizedBy);

        /// <summary>
        /// Get evidence destruction audit trail.
        /// </summary>
        Task<DestructionAuditTrail> GetDestructionAuditAsync(string evidenceId);

        /// <summary>
        /// Get list of evidence pending destruction approval.
        /// </summary>
        Task<IEnumerable<EvidenceRetentionInfo>> GetPendingDestructionAsync();
    }

    public class DestructionAuditTrail
    {
        public string EvidenceId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<DestructionApprovalStep> ApprovalSteps { get; set; } = new();
        public DateTime? DestroyedAt { get; set; }
        public string? DestructionMethod { get; set; }
        public string? VerificationHash { get; set; }
        public string? Witness { get; set; }
        public string? DestructionReport { get; set; }
    }

    public class DestructionApprovalStep
    {
        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
        public string ApprovedBy { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}
