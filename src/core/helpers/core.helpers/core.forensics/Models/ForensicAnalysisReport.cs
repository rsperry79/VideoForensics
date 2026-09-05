using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    /// <summary>
    /// Strongly-typed report of forensic analysis findings.
    /// Client application decides how to persist this report.
    /// </summary>
    public class ForensicAnalysisReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string? AnalyzedEvidenceId { get; set; }
        public string? AnalysisType { get; set; }
        public List<ForensicAnalysisResult> Findings { get; set; } = [];
        public string? Summary { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = [];

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
    }
}
