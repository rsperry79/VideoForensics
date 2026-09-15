using System;
using System.Collections.Generic;
using Xunit;
using VideoForensics.Forensics.Implementations;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Tests
{
    public class EvidenceValidatorTests
    {
        private readonly EvidenceValidator _validator = new();

        #region ValidateCompletenessAsync Tests

        [Fact]
        public async Task ValidateCompletenessAsync_WithCompleteEvidence_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractedData = new Dictionary<string, object> { { "frame", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithNullEvidence_ReturnsInvalid()
        {
            // Act
            var result = await _validator.ValidateCompletenessAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Contains("null", result.Errors[0].ToLowerInvariant());
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingSourceDeviceId_ReturnsError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = null,
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "examiner",
                ExtractedData = new Dictionary<string, object> { { "data", 1 } }
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("SourceDeviceId"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingEventTimestamp_ReturnsError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = null,
                EventType = "motion",
                ExtractionHandler = "examiner",
                ExtractedData = new Dictionary<string, object> { { "data", 1 } }
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("EventTimestamp"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingEventType_ReturnsError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = null,
                ExtractionHandler = "examiner",
                ExtractedData = new Dictionary<string, object> { { "data", 1 } }
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("EventType"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingExtractionHandler_ReturnsError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = null,
                ExtractedData = new Dictionary<string, object> { { "data", 1 } }
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("ExtractionHandler"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingExtractedData_ReturnsError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "examiner",
                ExtractedData = new Dictionary<string, object>()
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("ExtractedData"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMissingChecksums_ReturnsWarningNotError()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "examiner",
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string>()
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("checksums"));
        }

        [Fact]
        public async Task ValidateCompletenessAsync_WithMultipleMissingFields_ReturnsAllErrors()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = null,
                EventTimestamp = null,
                EventType = null,
                ExtractionHandler = null,
                ExtractedData = new Dictionary<string, object>()
            };

            // Act
            var result = await _validator.ValidateCompletenessAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Count >= 5);
        }

        #endregion

        #region ValidateIntegrityAsync Tests

        [Fact]
        public async Task ValidateIntegrityAsync_WithValidSha256Checksum_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithMultipleValidChecksums_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", "abc123" },
                    { "sha1", "def456" },
                    { "md5", "ghi789" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithBlake2bChecksum_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "blake2b", "blake2bhash123" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithNullEvidence_ReturnsInvalid()
        {
            // Act
            var result = await _validator.ValidateIntegrityAsync(null);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithNoChecksums_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>()
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("No checksums"));
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithNullChecksumsDictionary_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = null
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("No checksums"));
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithEmptyChecksumValue_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", "" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Empty checksum"));
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithUnrecognizedAlgorithm_ReturnsWarning()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", "abc123" },
                    { "custom-hash", "xyz789" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("Unrecognized"));
        }

        [Fact]
        public async Task ValidateIntegrityAsync_WithMixedValidAndInvalidChecksums_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                Checksums = new Dictionary<string, string>
                {
                    { "sha256", "valid123" },
                    { "sha1", "" }
                }
            };

            // Act
            var result = await _validator.ValidateIntegrityAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Empty checksum"));
        }

        #endregion

        #region ValidateComplianceAsync Tests

        [Fact]
        public async Task ValidateComplianceAsync_WithCertifiedExaminerAndRecentTimestamp_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithNamedCertifiedExaminer_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "Dr. Jane Smith - Certified Digital Forensics Examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithNullEvidence_ReturnsInvalid()
        {
            // Act
            var result = await _validator.ValidateComplianceAsync(null);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithMissingExtractionHandler_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = null,
                ExtractionTimestamp = DateTime.UtcNow
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("ExtractionHandler"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithUnknownExtractionHandler_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "unknown-examiner",
                ExtractionTimestamp = DateTime.UtcNow
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("not verified"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithUnverifiedExtractionHandler_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "unverified-handler",
                ExtractionTimestamp = DateTime.UtcNow
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("not verified"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithFutureExtractionTimestamp_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("cannot be in the future"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithMissingExtractionTimestamp_ReturnsInvalid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = default
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("ExtractionTimestamp"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithVeryOldExtractionTimestamp_ReturnsWarning()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddYears(-11)
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("10 years"));
        }

        [Fact]
        public async Task ValidateComplianceAsync_WithAlmostExactly10YearsOldTimestamp_ReturnsValid()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddYears(-10).AddSeconds(1) // Just before 10-year mark
            };

            // Act
            var result = await _validator.ValidateComplianceAsync(evidence);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Warnings);
        }

        #endregion

        #region GetValidationReportAsync Tests

        [Fact]
        public async Task GetValidationReportAsync_WithCompletelyValidEvidence_ReturnsValidReport()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-001",
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddDays(-1),
                ExtractedData = new Dictionary<string, object> { { "frame", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.IsValidOverall);
            Assert.Equal("evidence-001", report.EvidenceId);
            Assert.True(report.CompletenessValidation.IsValid);
            Assert.True(report.IntegrityValidation.IsValid);
            Assert.True(report.ComplianceValidation.IsValid);
            Assert.Empty(report.AllErrors);
            Assert.Empty(report.AllWarnings);
        }

        [Fact]
        public async Task GetValidationReportAsync_WithMissingCompleteness_ReturnsInvalidReport()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-002",
                SourceDeviceId = null, // Missing required field
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow,
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.False(report.IsValidOverall);
            Assert.False(report.CompletenessValidation.IsValid);
            Assert.NotEmpty(report.AllErrors);
        }

        [Fact]
        public async Task GetValidationReportAsync_WithMissingIntegrity_ReturnsInvalidReport()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-003",
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow,
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string>() // Empty checksums
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.False(report.IsValidOverall);
            Assert.False(report.IntegrityValidation.IsValid);
            Assert.NotEmpty(report.AllErrors);
        }

        [Fact]
        public async Task GetValidationReportAsync_WithMissingCompliance_ReturnsInvalidReport()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-004",
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "unknown-handler", // Not certified
                ExtractionTimestamp = DateTime.UtcNow,
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.False(report.IsValidOverall);
            Assert.False(report.ComplianceValidation.IsValid);
            Assert.NotEmpty(report.AllErrors);
        }

        [Fact]
        public async Task GetValidationReportAsync_AggregatesErrorsFromAllValidations()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-005",
                SourceDeviceId = null, // Completeness error
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "unknown", // Compliance error
                ExtractionTimestamp = DateTime.UtcNow,
                ExtractedData = new Dictionary<string, object>(),
                Checksums = new Dictionary<string, string>() // Integrity error
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.False(report.IsValidOverall);
            Assert.True(report.AllErrors.Count >= 3);
            Assert.False(report.CompletenessValidation.IsValid);
            Assert.False(report.IntegrityValidation.IsValid);
            Assert.False(report.ComplianceValidation.IsValid);
        }

        [Fact]
        public async Task GetValidationReportAsync_AggregatesWarningsFromAllValidations()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-006",
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow.AddYears(-11), // Old timestamp warning
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.True(report.IsValidOverall);
            Assert.NotEmpty(report.AllWarnings);
        }

        [Fact]
        public async Task GetValidationReportAsync_WithNullEvidence_ReturnsReportWithErrors()
        {
            // Act
            var report = await _validator.GetValidationReportAsync(null);

            // Assert
            Assert.False(report.IsValidOverall);
            Assert.NotEmpty(report.AllErrors);
        }

        [Fact]
        public async Task GetValidationReportAsync_PopulatesReportMetadata()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                EvidenceId = "evidence-007",
                SourceDeviceId = "device-001",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractionHandler = "certified-examiner",
                ExtractionTimestamp = DateTime.UtcNow,
                ExtractedData = new Dictionary<string, object> { { "data", 1 } },
                Checksums = new Dictionary<string, string> { { "sha256", "abc123" } }
            };

            // Act
            var report = await _validator.GetValidationReportAsync(evidence);

            // Assert
            Assert.NotNull(report.ReportId);
            Assert.NotEqual(default, report.GeneratedAt);
            Assert.Equal("evidence-007", report.EvidenceId);
        }

        #endregion
    }
}
