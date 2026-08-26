using VideoForensics.Data.Core.Models;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Service for generating forensic analysis reports from stored evidence data.</summary>
    public interface IReportGenerationService
    {
        /// <summary>Builds an evidence review report listing media items and their integrity status.</summary>
        Task<EvidenceReviewReport> BuildEvidenceReviewAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Builds a comprehensive forensic analysis report summarizing evidence across devices and timeframe.</summary>
        Task<ForensicAnalysisReport> BuildForensicAnalysisReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Builds a signal anomaly report analyzing device health metrics.</summary>
        Task<SignalAnomalyReport> BuildSignalAnomalyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Builds an access control report showing who accessed and exported evidence.</summary>
        Task<AccessControlReport> BuildAccessControlReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Builds a chain of custody report showing the complete audit trail for evidence.</summary>
        Task<ChainOfCustodyReport> BuildChainOfCustodyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>
        /// Writes a report to a file in the specified format.
        /// Applies redaction based on the configured RedactionLevel for export-bound reports.
        /// </summary>
        Task WriteReportAsync(
            object reportDto,
            string format, // "json", "xml", "csv"
            CancellationToken ct);
    }
}
