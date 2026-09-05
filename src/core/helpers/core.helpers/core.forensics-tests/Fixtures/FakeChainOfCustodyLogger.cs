using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Tests.Fixtures
{
    /// <summary>
    /// In-memory implementation of chain of custody logger for testing.
    /// </summary>
    public class FakeChainOfCustodyLogger : IChainOfCustodyLogger
    {
        private readonly Dictionary<string, List<ChainOfCustodyEntry>> _custodyChains = [];

        public Task LogEvidenceReceptionAsync(string evidenceId, string handler)
        {
            if (!_custodyChains.ContainsKey(evidenceId))
            {
                _custodyChains[evidenceId] = [];
            }

            _custodyChains[evidenceId].Add(new ChainOfCustodyEntry
            {
                EvidenceId = evidenceId,
                Handler = handler,
                Action = "reception",
                Timestamp = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public Task LogCustodyTransferAsync(string evidenceId, string fromHandler, string toHandler)
        {
            if (!_custodyChains.ContainsKey(evidenceId))
            {
                _custodyChains[evidenceId] = [];
            }

            _custodyChains[evidenceId].Add(new ChainOfCustodyEntry
            {
                EvidenceId = evidenceId,
                Handler = toHandler,
                Action = $"transfer from {fromHandler}",
                Timestamp = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public Task LogEvidenceAccessAsync(string evidenceId, string action, string handler)
        {
            if (!_custodyChains.ContainsKey(evidenceId))
            {
                _custodyChains[evidenceId] = [];
            }

            _custodyChains[evidenceId].Add(new ChainOfCustodyEntry
            {
                EvidenceId = evidenceId,
                Handler = handler,
                Action = action,
                Timestamp = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public Task<IEnumerable<ChainOfCustodyEntry>> GetChainOfCustodyAsync(string evidenceId)
        {
            return !_custodyChains.ContainsKey(evidenceId)
                ? Task.FromResult(Enumerable.Empty<ChainOfCustodyEntry>())
                : Task.FromResult(_custodyChains[evidenceId].OrderBy(e => e.Timestamp).AsEnumerable());
        }

        public Task<bool> VerifyCustodyIntegrityAsync(string evidenceId)
        {
            if (!_custodyChains.ContainsKey(evidenceId) || _custodyChains[evidenceId].Count == 0)
            {
                return Task.FromResult(false);
            }

            List<ChainOfCustodyEntry> entries = _custodyChains[evidenceId];
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].Timestamp < entries[i - 1].Timestamp)
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }

        public async Task<ChainOfCustodyReport> GetCustodyReportAsync(string evidenceId)
        {
            IEnumerable<ChainOfCustodyEntry> custody = await GetChainOfCustodyAsync(evidenceId);
            var custodyList = custody.ToList();
            var isVerified = await VerifyCustodyIntegrityAsync(evidenceId);

            return new ChainOfCustodyReport
            {
                EvidenceId = evidenceId,
                CustodyHistory = custodyList,
                CustodyIntegrityVerified = isVerified,
                TotalHandlers = custodyList.Select(e => e.Handler).Distinct().Count(),
                TotalAccessCount = custodyList.Count,
                EvidenceInitiallyReceived = custodyList.FirstOrDefault()?.Timestamp,
                EvidenceLastAccessed = custodyList.LastOrDefault()?.Timestamp
            };
        }
    }
}
