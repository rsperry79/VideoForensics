using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api;
using Ring.Api.Forensics.Models.Reports;

namespace Ring.Api.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of cryptographic report signing.
    /// To be completed with actual RSA signing and verification using the key storage provider.
    /// </summary>
    internal class ForensicReportSigner : IForensicReportSigner
    {
        public Task<ChainOfCustodyReport> SignChainOfCustodyReportAsync(
            ChainOfCustodyReport report,
            string keyId,
            string signingOfficer)
        {
            throw new NotImplementedException();
        }

        public Task<EvidenceValidationReport> SignValidationReportAsync(
            EvidenceValidationReport report,
            string keyId,
            string signingOfficer)
        {
            throw new NotImplementedException();
        }

        public Task<ForensicAnalysisReport> SignAnalysisReportAsync(
            ForensicAnalysisReport report,
            string keyId,
            string signingOfficer)
        {
            throw new NotImplementedException();
        }

        public Task<SignalAnomalyReport> SignSignalAnomalyReportAsync(
            SignalAnomalyReport report,
            string keyId,
            string signingOfficer)
        {
            throw new NotImplementedException();
        }

        public Task<bool> VerifyReportSignatureAsync<T>(T report, string certificateThumbprint) where T : class
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<T>> SignReportsAsync<T>(
            IEnumerable<T> reports,
            string keyId,
            string signingOfficer) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
