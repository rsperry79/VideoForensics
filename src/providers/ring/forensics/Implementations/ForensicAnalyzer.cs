using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Forensics.Models;
using VideoForensics.Providers.Ring.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of forensic analysis.
    /// To be completed with actual analysis logic.
    /// </summary>
    internal class ForensicAnalyzer : IForensicAnalyzer
    {
        public Task<ForensicAnalysisResult> AnalyzeEvidenceAsync(EvidenceMetadata evidence)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ForensicAnalysisResult>> DetectAnomaliesAsync(
            IEnumerable<EvidenceMetadata> evidenceSequence)
        {
            throw new NotImplementedException();
        }

        public Task<ForensicAnalysisReport> GetAnalysisReportAsync(
            IEnumerable<ForensicAnalysisResult> analysisResults,
            string? analysisType = null)
        {
            throw new NotImplementedException();
        }
    }
}
