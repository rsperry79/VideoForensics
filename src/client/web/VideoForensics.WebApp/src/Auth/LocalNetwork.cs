using System.Net;
using System.Net.Sockets;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// The local network: this machine (loopback) and the private address ranges (10/8, 172.16/12, 192.168/16, link-local, and the
    /// IPv6 unique-local and link-local ranges). Requests from here can use a password sign-in without going through the tunnel.
    /// </summary>
    public static class LocalNetwork
    {
        /// <summary>True for loopback and private addresses.</summary>
        public static bool IsLocalAddress(IPAddress address)
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
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return (bytes[0] & 0xFE) == 0xFC      // fc00::/7 unique local
                    || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80); // fe80::/10 link local
            }

            return false;
        }

        /// <summary>True for the name localhost, or for an IP address literal that is a local address.</summary>
        public static bool IsLocalHostName(string host)
        {
            string trimmed = host.Trim().TrimStart('[').TrimEnd(']');
            if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(trimmed, out IPAddress? address) && IsLocalAddress(address);
        }
    }
}
