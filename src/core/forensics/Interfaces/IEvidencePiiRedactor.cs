using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Redacts or removes personally identifiable information (PII) from forensic evidence and reports.
    /// Protects victim identity and privacy when reports are shared outside secure channels.
    /// </summary>
    public interface IEvidencePiiRedactor
    {
        /// <summary>
        /// Redact victim PII from a chain of custody report.
        /// </summary>
        Task<ChainOfCustodyReport> RedactChainOfCustodyAsync(
            ChainOfCustodyReport report,
            RedactionOptions options);

        /// <summary>
        /// Redact victim PII from evidence validation report.
        /// </summary>
        Task<EvidenceValidationReport> RedactValidationReportAsync(
            EvidenceValidationReport report,
            RedactionOptions options);

        /// <summary>
        /// Redact victim PII from forensic analysis report.
        /// </summary>
        Task<ForensicAnalysisReport> RedactAnalysisReportAsync(
            ForensicAnalysisReport report,
            RedactionOptions options);

        /// <summary>
        /// Redact victim PII from signal anomaly report.
        /// </summary>
        Task<SignalAnomalyReport> RedactSignalAnomalyReportAsync(
            SignalAnomalyReport report,
            RedactionOptions options);

        /// <summary>
        /// Generate fully anonymized report (no victim identifiers at all).
        /// </summary>
        Task<T> GenerateAnonymizedReportAsync<T>(T report) where T : class;

        /// <summary>
        /// Batch redact multiple reports.
        /// </summary>
        Task<IEnumerable<T>> RedactReportsAsync<T>(
            IEnumerable<T> reports,
            RedactionOptions options) where T : class;
    }

    public class RedactionOptions
    {
        public bool RedactVictimName { get; set; } = true;
        public bool RedactPhoneNumber { get; set; } = true;
        public bool RedactAddress { get; set; } = true;
        public bool RedactEmail { get; set; } = true;
        public bool RedactDeviceSerialNumbers { get; set; }
        public bool RedactHandlerNames { get; set; }
        public bool RedactAccessLogs { get; set; }
        public bool RedactTimestamps { get; set; }
        public bool FullyAnonymize { get; set; }
        public List<string> CustomSensitiveFields { get; set; } = new();
        public string? ReplacementString { get; set; } = "[REDACTED]";
    }

    public class RedactionAudit
    {
        public string ReportId { get; set; } = string.Empty;
        public DateTime RedactedAt { get; set; } = DateTime.UtcNow;
        public string RedactedBy { get; set; } = string.Empty;
        public List<string> FieldsRedacted { get; set; } = new();
        public int TotalRedactions { get; set; }
        public bool IsFullyAnonymized { get; set; }
    }
}
