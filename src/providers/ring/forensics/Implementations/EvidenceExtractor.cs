using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Forensics.Models;

namespace VideoForensics.Providers.Ring.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of evidence extraction.
    /// To be completed with actual extraction logic.
    /// </summary>
    internal class EvidenceExtractor : IEvidenceExtractor
    {
        public Task<EvidenceMetadata> ExtractEvidenceAsync(DoorbotHistoryEvent @event)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EvidenceMetadata>> ExtractEvidenceTimeSeriesAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            ForensicsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public EvidenceIntegrityStatus ValidateExtraction()
        {
            throw new NotImplementedException();
        }
    }
}
