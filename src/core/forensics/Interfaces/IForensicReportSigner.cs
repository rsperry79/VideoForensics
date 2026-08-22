using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Cryptographically signs forensic reports to prove they haven't been tampered with.
    /// Uses RSA-2048 signatures with SHA-256 hashing.
    /// </summary>
    public interface IForensicReportSigner
    {
        /// <summary>
        /// Sign a chain of custody report.
        /// </summary>
        Task<ChainOfCustodyReport> SignChainOfCustodyReportAsync(
            ChainOfCustodyReport report,
            string keyId,
            string signingOfficer);

        /// <summary>
        /// Sign an evidence validation report.
        /// </summary>
        Task<EvidenceValidationReport> SignValidationReportAsync(
            EvidenceValidationReport report,
            string keyId,
            string signingOfficer);

        /// <summary>
        /// Sign a forensic analysis report.
        /// </summary>
        Task<ForensicAnalysisReport> SignAnalysisReportAsync(
            ForensicAnalysisReport report,
            string keyId,
            string signingOfficer);

        /// <summary>
        /// Sign a signal anomaly report.
        /// </summary>
        Task<SignalAnomalyReport> SignSignalAnomalyReportAsync(
            SignalAnomalyReport report,
            string keyId,
            string signingOfficer);

        /// <summary>
        /// Verify a report's signature against the signing certificate.
        /// </summary>
        Task<bool> VerifyReportSignatureAsync<T>(T report, string certificateThumbprint) where T : class;

        /// <summary>
        /// Batch sign multiple reports.
        /// </summary>
        Task<IEnumerable<T>> SignReportsAsync<T>(
            IEnumerable<T> reports,
            string keyId,
            string signingOfficer) where T : class;
    }
}
