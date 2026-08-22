using System;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Forensics.Models.Reports
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
        public List<ForensicAnalysisResult> Findings { get; set; } = new();
        public string? Summary { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
    }
}
