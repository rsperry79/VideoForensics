using System;

namespace Ring.Api.Forensics.Models
{
    public class AccessAnomaly
    {
        public string EvidenceId { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public AccessAnomalySeverity Severity { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string? AffectedUserId { get; set; }
        public int FailedAttemptCount { get; set; }
        public bool RequiresInvestigation { get; set; }
    }

    public enum AccessAnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
