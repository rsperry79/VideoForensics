using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Forensics.Models;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Forensics
{
    /// <summary>
    /// Performs forensic analysis on extracted evidence, detecting patterns,
    /// anomalies, and generating investigative reports.
    /// Reports are returned as strongly-typed DTOs; client application
    /// decides how to persist them (JSON, database, etc).
    /// </summary>
    public interface IForensicAnalyzer
    {
        /// <summary>
        /// Analyzes evidence for common forensic indicators.
        /// </summary>
        /// <param name="evidence">Evidence to analyze.</param>
        /// <returns>Analysis results including findings and recommendations.</returns>
        Task<ForensicAnalysisResult> AnalyzeEvidenceAsync(EvidenceMetadata evidence);

        /// <summary>
        /// Detects temporal anomalies or patterns in a sequence of events.
        /// </summary>
        /// <param name="evidenceSequence">Time-ordered evidence items.</param>
        /// <returns>Detected patterns and anomalies.</returns>
        Task<IEnumerable<ForensicAnalysisResult>> DetectAnomaliesAsync(
            IEnumerable<EvidenceMetadata> evidenceSequence);

        /// <summary>
        /// Retrieves a strongly-typed forensic analysis report from analyzed evidence.
        /// Client application decides how to persist this report (JSON, database, etc).
        /// </summary>
        /// <param name="analysisResults">Results from evidence analysis.</param>
        /// <param name="analysisType">Type of analysis performed (optional descriptive label).</param>
        /// <returns>Strongly-typed forensic analysis report.</returns>
        Task<ForensicAnalysisReport> GetAnalysisReportAsync(
            IEnumerable<ForensicAnalysisResult> analysisResults,
            string? analysisType = null);
    }
}
