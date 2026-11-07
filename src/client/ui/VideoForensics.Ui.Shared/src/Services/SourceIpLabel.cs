using System.Net;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Display text for a source IP address on the security pages. Loopback addresses (::1, 127.0.0.1 and
    /// ::ffff:127.0.0.1) show as the localized LocalHost label. Display only: the stored audit value is not changed.
    /// </summary>
    public static class SourceIpLabel
    {
        /// <summary>
        /// Returns <paramref name="localHostLabel"/> when <paramref name="ip"/> is a loopback address. Any other value,
        /// including null, empty or unparseable text, is returned unchanged.
        /// </summary>
        /// <remarks>
        /// Parses with <see cref="IPAddress.TryParse(string?, out IPAddress?)"/> and checks <see cref="IPAddress.IsLoopback(IPAddress)"/>,
        /// so the IPv4-mapped IPv6 form is recognised. A string comparison would miss it.
        /// </remarks>
        /// <param name="ip">The stored source address, or null.</param>
        /// <param name="localHostLabel">The localized label to show for a loopback address.</param>
        /// <returns>The label for a loopback address, otherwise <paramref name="ip"/>.</returns>
        public static string? Format(string? ip, string localHostLabel)
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out IPAddress? address))
            {
                return ip;
            }

            return IPAddress.IsLoopback(address) ? localHostLabel : ip;
        }
    }
}
