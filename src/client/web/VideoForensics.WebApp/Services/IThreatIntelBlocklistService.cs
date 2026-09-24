using System.Net;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Service for checking if an IP address is blocked by threat intelligence feeds (e.g., Spamhaus, Emerging Threats).
    /// </summary>
    public interface IThreatIntelBlocklistService
    {
        /// <summary>
        /// Checks if the given IP address is on a threat intelligence blocklist.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the IP is blocked by threat intelligence, false otherwise.</returns>
        Task<bool> IsIpBlockedAsync(IPAddress ipAddress, CancellationToken ct);
    }
}
