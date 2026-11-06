using System.Net;
using System.Net.Sockets;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Whether a host name is this machine or the private network. The same rule as the server's local-network check, so the
    /// sign-in page and the server agree on which requests are local.
    /// </summary>
    public static class LocalNetworkAddress
    {
        /// <summary>True for localhost, loopback, and private and link-local address literals.</summary>
        public static bool IsLocalHost(string host)
        {
            string trimmed = host.Trim().TrimStart('[').TrimEnd(']');
            return trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(trimmed, out IPAddress? address) && IsLocalAddress(address));
        }

        private static bool IsLocalAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            byte[] bytes = address.GetAddressBytes();
            return address.AddressFamily == AddressFamily.InterNetwork
                ? bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254)
                : address.AddressFamily == AddressFamily.InterNetworkV6 && ((bytes[0] & 0xFE) == 0xFC
                    || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80));
        }
    }
}
