using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Validates evidence for authenticity, completeness, and compliance
    /// with forensic analysis standards.
    /// </summary>
    public class EvidenceValidator : IEvidenceValidator
    {
        /// <summary>
        /// Validates evidence metadata completeness.
        /// Checks for required fields: SourceDeviceId, EventTimestamp, EventType, ExtractionHandler.
        /// </summary>
        public Task<ValidationResult> ValidateCompletenessAsync(EvidenceMetadata evidence)
        {
            var result = new ValidationResult { IsValid = true, Errors = new(), Warnings = new() };

            if (evidence == null)
            {
                result.IsValid = false;
                result.Errors.Add("Evidence metadata is null.");
                return Task.FromResult(result);
            }

            if (string.IsNullOrWhiteSpace(evidence.SourceDeviceId))
            {
                result.IsValid = false;
                result.Errors.Add("SourceDeviceId is required.");
            }

            if (evidence.EventTimestamp == null || evidence.EventTimestamp == default)
            {
                result.IsValid = false;
                result.Errors.Add("EventTimestamp is required.");
            }

            if (string.IsNullOrWhiteSpace(evidence.EventType))
            {
                result.IsValid = false;
                result.Errors.Add("EventType is required.");
            }

            if (string.IsNullOrWhiteSpace(evidence.ExtractionHandler))
            {
                result.IsValid = false;
                result.Errors.Add("ExtractionHandler is required.");
            }

            if (evidence.ExtractedData == null || evidence.ExtractedData.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("ExtractedData must contain at least one entry.");
            }

            if (evidence.Checksums == null || evidence.Checksums.Count == 0)
            {
                result.Warnings.Add("No checksums provided for integrity verification.");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Verifies digital integrity (checksums, signatures).
        /// Checks if checksums are present and not empty.
        /// </summary>
        public Task<ValidationResult> ValidateIntegrityAsync(EvidenceMetadata evidence)
        {
            var result = new ValidationResult { IsValid = true, Errors = new(), Warnings = new() };

            if (evidence == null)
            {
                result.IsValid = false;
                result.Errors.Add("Evidence metadata is null.");
                return Task.FromResult(result);
            }

            if (evidence.Checksums == null || evidence.Checksums.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No checksums provided for integrity verification.");
                return Task.FromResult(result);
            }

            // Validate that all checksums have non-empty values
            var emptyChecksums = evidence.Checksums.Where(cs => string.IsNullOrWhiteSpace(cs.Value)).ToList();
            if (emptyChecksums.Count > 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Empty checksum values found for: {string.Join(", ", emptyChecksums.Select(cs => cs.Key))}");
            }

            // Validate that checksums have recognized hash algorithms
            var validAlgorithms = new[] { "sha256", "sha1", "md5", "blake2b" };
            var invalidAlgorithms = evidence.Checksums.Keys
                .Where(key => !validAlgorithms.Contains(key.ToLowerInvariant()))
                .ToList();

            if (invalidAlgorithms.Count > 0)
            {
                result.Warnings.Add($"Unrecognized hash algorithms: {string.Join(", ", invalidAlgorithms)}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Checks compliance with forensic standards and legal requirements.
        /// Validates that extraction was handled by a certified examiner and extraction
        /// timestamp is reasonable (within the last 10 years).
        /// </summary>
        public Task<ValidationResult> ValidateComplianceAsync(EvidenceMetadata evidence)
        {
            var result = new ValidationResult { IsValid = true, Errors = new(), Warnings = new() };

            if (evidence == null)
            {
                result.IsValid = false;
                result.Errors.Add("Evidence metadata is null.");
                return Task.FromResult(result);
            }

            // Check if ExtractionHandler looks like a certified examiner
            if (string.IsNullOrWhiteSpace(evidence.ExtractionHandler))
            {
                result.IsValid = false;
                result.Errors.Add("ExtractionHandler is required for compliance verification.");
            }
            else if (evidence.ExtractionHandler.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                     evidence.ExtractionHandler.Contains("unverified", StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.Errors.Add("Extraction handler is not verified/certified.");
            }

            // Check extraction timestamp is reasonable (not in the future, not too old)
            if (evidence.ExtractionTimestamp == default)
            {
                result.IsValid = false;
                result.Errors.Add("ExtractionTimestamp is required for compliance verification.");
            }
            else
            {
                var now = DateTime.UtcNow;
                if (evidence.ExtractionTimestamp > now)
                {
                    result.IsValid = false;
                    result.Errors.Add("ExtractionTimestamp cannot be in the future.");
                }

                var tenYearsAgo = now.AddYears(-10);
                if (evidence.ExtractionTimestamp < tenYearsAgo)
                {
                    result.Warnings.Add("Evidence is older than 10 years; chain of custody may be questioned.");
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Retrieves a strongly-typed evidence validation report with all validation results.
        /// Includes completeness, integrity, and compliance checks.
        /// </summary>
        public async Task<EvidenceValidationReport> GetValidationReportAsync(EvidenceMetadata evidence)
        {
            var completenessResult = await ValidateCompletenessAsync(evidence);
            var integrityResult = await ValidateIntegrityAsync(evidence);
            var complianceResult = await ValidateComplianceAsync(evidence);

            var allErrors = new List<string>();
            allErrors.AddRange(completenessResult.Errors);
            allErrors.AddRange(integrityResult.Errors);
            allErrors.AddRange(complianceResult.Errors);

            var allWarnings = new List<string>();
            allWarnings.AddRange(completenessResult.Warnings);
            allWarnings.AddRange(integrityResult.Warnings);
            allWarnings.AddRange(complianceResult.Warnings);

            var isValidOverall = completenessResult.IsValid && integrityResult.IsValid && complianceResult.IsValid;

            var report = new EvidenceValidationReport
            {
                EvidenceId = evidence?.EvidenceId ?? string.Empty,
                IsValidOverall = isValidOverall,
                CompletenessValidation = completenessResult,
                IntegrityValidation = integrityResult,
                ComplianceValidation = complianceResult,
                AllErrors = allErrors,
                AllWarnings = allWarnings
            };

            return report;
        }
    }
}
