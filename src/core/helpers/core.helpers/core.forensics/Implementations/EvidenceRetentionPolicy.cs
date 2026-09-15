using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// In-memory implementation of evidence retention policy management.
    /// Maintains retention periods, destruction authorization workflows, and audit trails
    /// for forensic evidence.
    /// </summary>
    internal class EvidenceRetentionPolicy : IEvidenceRetentionPolicy
    {
        private readonly Dictionary<string, EvidenceRetentionInfo> _retentionInfo = [];
        private readonly Dictionary<string, DestructionAuditTrail> _auditTrails = [];
        private readonly HashSet<string> _onHoldIds = [];

        public Task SetRetentionPeriodAsync(string evidenceId, TimeSpan retentionPeriod, string reason, string authorizedBy)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));
            if (string.IsNullOrWhiteSpace(authorizedBy))
                throw new ArgumentException("Authorized by cannot be null or empty.", nameof(authorizedBy));

            var now = DateTime.UtcNow;
            var info = new EvidenceRetentionInfo
            {
                EvidenceId = evidenceId,
                RetentionStartDate = now,
                RetentionEndDate = now.Add(retentionPeriod),
                RetentionPeriod = retentionPeriod,
                Status = RetentionStatus.Active,
                RetentionReason = reason
            };

            _retentionInfo[evidenceId] = info;

            // Initialize audit trail
            if (!_auditTrails.ContainsKey(evidenceId))
            {
                _auditTrails[evidenceId] = new DestructionAuditTrail
                {
                    EvidenceId = evidenceId,
                    CreatedAt = now,
                    ApprovalSteps = []
                };
            }

            return Task.CompletedTask;
        }

        public Task<EvidenceRetentionInfo> GetRetentionStatusAsync(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));

            if (!_retentionInfo.ContainsKey(evidenceId))
                return Task.FromResult<EvidenceRetentionInfo>(null);

            return Task.FromResult(_retentionInfo[evidenceId]);
        }

        public Task<bool> CanDestroyEvidenceAsync(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));

            if (!_retentionInfo.ContainsKey(evidenceId))
                return Task.FromResult(false);

            var info = _retentionInfo[evidenceId];

            // Cannot destroy if on legal hold
            if (info.Status == RetentionStatus.OnHold)
                return Task.FromResult(false);

            // Can only destroy if status is ApprovedForDestruction
            if (info.Status != RetentionStatus.ApprovedForDestruction)
                return Task.FromResult(false);

            // Check if retention period has expired
            bool expired = DateTime.UtcNow >= info.RetentionEndDate;
            return Task.FromResult(expired);
        }

        public Task RequestDestructionApprovalAsync(string evidenceId, string requestedBy, string reason)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(requestedBy))
                throw new ArgumentException("Requested by cannot be null or empty.", nameof(requestedBy));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));

            if (!_retentionInfo.ContainsKey(evidenceId))
                throw new KeyNotFoundException($"Evidence {evidenceId} not found.");

            var info = _retentionInfo[evidenceId];
            info.Status = RetentionStatus.PendingApprovalForDestruction;

            // Record in audit trail
            if (_auditTrails.ContainsKey(evidenceId))
            {
                _auditTrails[evidenceId].ApprovalSteps.Add(new DestructionApprovalStep
                {
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = requestedBy,
                    Reason = reason,
                    IsApproved = false
                });
            }

            return Task.CompletedTask;
        }

        public Task ApproveDestructionAsync(string evidenceId, string approvedBy, string method)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(approvedBy))
                throw new ArgumentException("Approved by cannot be null or empty.", nameof(approvedBy));
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("Method cannot be null or empty.", nameof(method));

            if (!_retentionInfo.ContainsKey(evidenceId))
                throw new KeyNotFoundException($"Evidence {evidenceId} not found.");

            var info = _retentionInfo[evidenceId];
            info.Status = RetentionStatus.ApprovedForDestruction;
            info.DestructionAuthorizedBy = approvedBy;
            info.DestructionMethod = method;
            info.ApprovedForDestructionAt = DateTime.UtcNow;

            // Record in audit trail
            if (_auditTrails.ContainsKey(evidenceId))
            {
                _auditTrails[evidenceId].ApprovalSteps.Add(new DestructionApprovalStep
                {
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = approvedBy,
                    IsApproved = true
                });
            }

            return Task.CompletedTask;
        }

        public Task DestroyEvidenceAsync(string evidenceId, string destructionMethod, string verificationHash)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(destructionMethod))
                throw new ArgumentException("Destruction method cannot be null or empty.", nameof(destructionMethod));
            if (string.IsNullOrWhiteSpace(verificationHash))
                throw new ArgumentException("Verification hash cannot be null or empty.", nameof(verificationHash));

            if (!_retentionInfo.ContainsKey(evidenceId))
                throw new KeyNotFoundException($"Evidence {evidenceId} not found.");

            var now = DateTime.UtcNow;
            var info = _retentionInfo[evidenceId];
            info.Status = RetentionStatus.Destroyed;
            info.DestructedAt = now;
            info.DestructionMethod = destructionMethod;
            info.DestructionVerificationHash = verificationHash;

            // Record in audit trail
            if (_auditTrails.ContainsKey(evidenceId))
            {
                var audit = _auditTrails[evidenceId];
                audit.DestroyedAt = now;
                audit.DestructionMethod = destructionMethod;
                audit.VerificationHash = verificationHash;
            }

            return Task.CompletedTask;
        }

        public Task ExtendRetentionAsync(string evidenceId, TimeSpan additionalPeriod, string reason, string authorizedBy)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));
            if (string.IsNullOrWhiteSpace(authorizedBy))
                throw new ArgumentException("Authorized by cannot be null or empty.", nameof(authorizedBy));

            if (!_retentionInfo.ContainsKey(evidenceId))
                throw new KeyNotFoundException($"Evidence {evidenceId} not found.");

            var info = _retentionInfo[evidenceId];
            var currentStatus = info.Status;

            // Extend the retention period
            info.RetentionEndDate = info.RetentionEndDate.Add(additionalPeriod);
            info.RetentionPeriod = info.RetentionPeriod.Add(additionalPeriod);
            info.LastExtendedAt = DateTime.UtcNow;

            // Preserve OnHold status; if not on hold, revert to Active if applicable
            if (currentStatus != RetentionStatus.OnHold &&
                currentStatus != RetentionStatus.ApprovedForDestruction &&
                currentStatus != RetentionStatus.Destroyed &&
                currentStatus != RetentionStatus.PendingApprovalForDestruction)
            {
                info.Status = RetentionStatus.Active;
            }

            return Task.CompletedTask;
        }

        public Task<DestructionAuditTrail> GetDestructionAuditAsync(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Evidence ID cannot be null or empty.", nameof(evidenceId));

            if (!_auditTrails.ContainsKey(evidenceId))
            {
                return Task.FromResult(new DestructionAuditTrail
                {
                    EvidenceId = evidenceId,
                    CreatedAt = DateTime.UtcNow,
                    ApprovalSteps = []
                });
            }

            return Task.FromResult(_auditTrails[evidenceId]);
        }

        public Task<IEnumerable<EvidenceRetentionInfo>> GetPendingDestructionAsync()
        {
            var pending = _retentionInfo.Values
                .Where(info => info.Status == RetentionStatus.PendingApprovalForDestruction)
                .ToList();

            return Task.FromResult(pending.AsEnumerable());
        }
    }
}
