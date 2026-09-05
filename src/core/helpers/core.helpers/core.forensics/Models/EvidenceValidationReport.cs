using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    /// <summary>
    /// Strongly-typed report of evidence validation results.
    /// Client application decides how to persist this report.
    /// </summary>
    public class EvidenceValidationReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string EvidenceId { get; set; } = string.Empty;
        public bool IsValidOverall { get; set; }
        public ValidationResult CompletenessValidation { get; set; } = new();
        public ValidationResult IntegrityValidation { get; set; } = new();
        public ValidationResult ComplianceValidation { get; set; } = new();
        public List<string> AllErrors { get; set; } = [];
        public List<string> AllWarnings { get; set; } = [];
        public string? CertificationStatement { get; set; }
        public string? ValidatedBy { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = [];

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
        public List<string> ComplianceFrameworksApplied { get; set; } = [];
        public string? LegalJurisdiction { get; set; }
    }
}
