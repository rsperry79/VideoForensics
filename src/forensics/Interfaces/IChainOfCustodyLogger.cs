using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Forensics.Models;
using Ring.Api.Forensics.Models.Reports;

namespace Ring.Api.Forensics
{
    /// <summary>
    /// Maintains chain of custody records for evidence handling, tracking
    /// all access, modifications, and transfers of forensic evidence.
    /// Reports are returned as strongly-typed DTOs; client application
    /// decides how to persist them (JSON, database, etc).
    /// </summary>
    public interface IChainOfCustodyLogger
    {
        /// <summary>
        /// Logs evidence reception into custody.
        /// </summary>
        Task LogEvidenceReceptionAsync(string evidenceId, string handler);

        /// <summary>
        /// Logs a custody transfer between handlers.
        /// </summary>
        Task LogCustodyTransferAsync(string evidenceId, string fromHandler, string toHandler);

        /// <summary>
        /// Logs evidence analysis or processing action.
        /// </summary>
        Task LogEvidenceAccessAsync(string evidenceId, string action, string handler);

        /// <summary>
        /// Retrieves complete chain of custody for evidence.
        /// </summary>
        /// <returns>Chronological list of custody entries.</returns>
        Task<IEnumerable<ChainOfCustodyEntry>> GetChainOfCustodyAsync(string evidenceId);

        /// <summary>
        /// Verifies integrity of chain of custody record.
        /// </summary>
        /// <returns>True if chain is unbroken and valid.</returns>
        Task<bool> VerifyCustodyIntegrityAsync(string evidenceId);

        /// <summary>
        /// Retrieves a strongly-typed chain of custody report for evidence.
        /// Includes complete custody history and integrity verification.
        /// Client application decides how to persist this report (JSON, database, etc).
        /// </summary>
        /// <param name="evidenceId">ID of evidence to generate report for.</param>
        /// <returns>Strongly-typed chain of custody report.</returns>
        Task<ChainOfCustodyReport> GetCustodyReportAsync(string evidenceId);
    }
}
