using System.Net;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Service for performing GeoIP lookups to resolve the country code associated with an IP address.
    /// Returns the ISO 3166-1 alpha-2 country code (e.g., "US", "GB"), or null if the lookup fails or is unavailable.
    /// </summary>
    public interface IGeoIpLookupService
    {
        /// <summary>
        /// Attempts to resolve the ISO 3166-1 alpha-2 country code for the given IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to look up (IPv4 or IPv6).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The two-character ISO country code, or null if the lookup fails or is unavailable (never throws).</returns>
        Task<string?> LookupCountryCodeAsync(IPAddress? ipAddress, CancellationToken ct);
    }
}
