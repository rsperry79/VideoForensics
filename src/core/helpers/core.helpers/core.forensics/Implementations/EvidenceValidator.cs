using System;
using System.Threading.Tasks;
using VideoForensics.Forensics.Models;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of evidence validation.
    /// To be completed with actual validation logic.
    /// </summary>
    internal class EvidenceValidator : IEvidenceValidator
    {
        public Task<ValidationResult> ValidateCompletenessAsync(EvidenceMetadata evidence)
        {
            throw new NotImplementedException();
        }

        public Task<ValidationResult> ValidateIntegrityAsync(EvidenceMetadata evidence)
        {
            throw new NotImplementedException();
        }

        public Task<ValidationResult> ValidateComplianceAsync(EvidenceMetadata evidence)
        {
            throw new NotImplementedException();
        }

        public Task<EvidenceValidationReport> GetValidationReportAsync(EvidenceMetadata evidence)
        {
            throw new NotImplementedException();
        }
    }
}
