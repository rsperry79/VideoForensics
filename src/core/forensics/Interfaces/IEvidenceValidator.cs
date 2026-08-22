using System.Threading.Tasks;
using VideoForensics.Forensics.Models;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Forensics
{
    /// <summary>
    /// Validates evidence for authenticity, completeness, and compliance
    /// with forensic analysis standards.
    /// Reports are returned as strongly-typed DTOs; client application
    /// decides how to persist them (JSON, database, etc).
    /// </summary>
    public interface IEvidenceValidator
    {
        /// <summary>
        /// Validates evidence metadata completeness.
        /// </summary>
        /// <returns>Validation result with any missing fields noted.</returns>
        Task<ValidationResult> ValidateCompletenessAsync(EvidenceMetadata evidence);

        /// <summary>
        /// Verifies digital integrity (checksums, signatures).
        /// </summary>
        /// <returns>Integrity check result.</returns>
        Task<ValidationResult> ValidateIntegrityAsync(EvidenceMetadata evidence);

        /// <summary>
        /// Checks compliance with forensic standards and legal requirements.
        /// </summary>
        /// <returns>Compliance check result and any violations.</returns>
        Task<ValidationResult> ValidateComplianceAsync(EvidenceMetadata evidence);

        /// <summary>
        /// Retrieves a strongly-typed evidence validation report with all validation results.
        /// Includes completeness, integrity, and compliance checks.
        /// Client application decides how to persist this report (JSON, database, etc).
        /// </summary>
        /// <param name="evidence">Evidence to validate and report on.</param>
        /// <returns>Strongly-typed evidence validation report.</returns>
        Task<EvidenceValidationReport> GetValidationReportAsync(EvidenceMetadata evidence);
    }
}
