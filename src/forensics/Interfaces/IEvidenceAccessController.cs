using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Forensics.Models;
using Ring.Api.Forensics.Implementations;

namespace Ring.Api
{
    /// <summary>
    /// Controls and audits access to evidence with anomaly detection.
    /// Ensures that only authorized personnel access evidence and detects suspicious access patterns.
    /// </summary>
    public interface IEvidenceAccessController
    {
        /// <summary>
        /// Check if a user is allowed to access evidence based on role and authorization.
        /// </summary>
        Task<bool> IsAccessAllowedAsync(string evidenceId, string userId, string userRole, string accessReason);

        /// <summary>
        /// Detect suspicious access patterns (failed attempts, off-hours, multiple rapid accesses, unusual jurisdictions).
        /// </summary>
        Task<IEnumerable<AccessAnomaly>> DetectSuspiciousAccessAsync(string evidenceId);

        /// <summary>
        /// Get all access attempts (successful and failed) for evidence.
        /// </summary>
        Task<IEnumerable<AccessLog>> GetAccessHistoryAsync(string evidenceId);

        /// <summary>
        /// Flag evidence access for administrative review (potential tampering attempt).
        /// </summary>
        Task FlagAccessForReviewAsync(string evidenceId, string reason);

        /// <summary>
        /// Get risk assessment for a user based on access history and patterns.
        /// </summary>
        Task<UserAccessRiskProfile> GetUserAccessRiskAsync(string userId);
    }

    public class AccessLog
    {
        public string AccessId { get; set; } = Guid.NewGuid().ToString();
        public string EvidenceId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
        public bool AccessGranted { get; set; }
        public string? AccessReason { get; set; }
        public string? DenialReason { get; set; }
        public bool IsSuspicious { get; set; }
        public string? UserLocation { get; set; }
        public string? IpAddress { get; set; }
    }

    public class UserAccessRiskProfile
    {
        public string UserId { get; set; } = string.Empty;
        public double RiskScore { get; set; } // 0-1
        public int FailedAccessAttempts { get; set; }
        public int OffHoursAccessCount { get; set; }
        public int RapidAccessCount { get; set; }
        public List<string> FlaggedAccessReasons { get; set; } = new();
        public DateTime? LastSuspiciousActivity { get; set; }
        public string? RecommendedAction { get; set; }
    }
}
