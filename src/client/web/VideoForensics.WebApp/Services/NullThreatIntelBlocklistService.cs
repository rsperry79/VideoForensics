using System.Net;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Null implementation of IThreatIntelBlocklistService that always returns false (never blocks).
    /// Used as a placeholder until the actual threat intelligence feed fetching background job is implemented.
    /// </summary>
    public class NullThreatIntelBlocklistService : IThreatIntelBlocklistService
    {
        public Task<bool> IsIpBlockedAsync(IPAddress ipAddress, CancellationToken ct)
        {
            // Null implementation: never block
            return Task.FromResult(false);
        }
    }
}
