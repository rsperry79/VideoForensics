using System.Net;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Service for checking if an IP address is in a manually-banned CIDR range.
    /// </summary>
    public interface IBannedIpMatchService
    {
        /// <summary>
        /// Checks if the given IP address is within any active banned IP range.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the IP is within any active banned range, false otherwise.</returns>
        Task<bool> IsIpBannedAsync(IPAddress ipAddress, CancellationToken ct);
    }
}
