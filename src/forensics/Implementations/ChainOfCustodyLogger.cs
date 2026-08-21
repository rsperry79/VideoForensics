using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ring.Api.Forensics.Models;
using Ring.Api.Forensics.Models.Reports;

namespace Ring.Api.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of chain of custody logging.
    /// To be completed with actual logging logic.
    /// </summary>
    internal class ChainOfCustodyLogger : IChainOfCustodyLogger
    {
        public Task LogEvidenceReceptionAsync(string evidenceId, string handler)
        {
            throw new NotImplementedException();
        }

        public Task LogCustodyTransferAsync(string evidenceId, string fromHandler, string toHandler)
        {
            throw new NotImplementedException();
        }

        public Task LogEvidenceAccessAsync(string evidenceId, string action, string handler)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ChainOfCustodyEntry>> GetChainOfCustodyAsync(string evidenceId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> VerifyCustodyIntegrityAsync(string evidenceId)
        {
            throw new NotImplementedException();
        }

        public Task<ChainOfCustodyReport> GetCustodyReportAsync(string evidenceId)
        {
            throw new NotImplementedException();
        }
    }
}
