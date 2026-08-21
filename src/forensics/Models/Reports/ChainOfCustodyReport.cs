using System;
using System.Collections.Generic;

namespace Ring.Api.Forensics.Models.Reports
{
    /// <summary>
    /// Strongly-typed report documenting chain of custody for evidence.
    /// Client application decides how to persist this report.
    /// </summary>
    public class ChainOfCustodyReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string EvidenceId { get; set; } = string.Empty;
        public bool CustodyIntegrityVerified { get; set; }
        public List<ChainOfCustodyEntry> CustodyHistory { get; set; } = new();
        public DateTime? EvidenceInitiallyReceived { get; set; }
        public DateTime? EvidenceLastAccessed { get; set; }
        public int TotalHandlers { get; set; }
        public int TotalAccessCount { get; set; }
        public List<string> IntegrityIssues { get; set; } = new();
        public string? LegalStatement { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
        public bool IsChainTampered { get; set; }
    }
}
