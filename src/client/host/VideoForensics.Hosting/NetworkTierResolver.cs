using System.Net;
using Microsoft.AspNetCore.Http;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Resolves which network tier (plan §5.2) an individual HTTP request actually arrived via -
    /// used both to enforce the currently-configured tier and, critically, for the SuperAdmin
    /// physical-presence check (§5.10), which must reflect the REAL path a specific request took,
    /// not merely which tiers are enabled overall.
    ///
    /// ESCALATION-FLAGGED LOGIC (plan §10): traffic arriving through the Internet tier's Cloudflare
    /// Tunnel all originates from Cloudflare's own edge IP unless the app explicitly reads the
    /// CF-Connecting-IP header - naively trusting that header from ANY caller reintroduces IP
    /// spoofing outright (an attacker connecting directly, bypassing the tunnel, could claim to be
    /// any IP they like). The header is only honored when the immediate TCP peer is a real
    /// Cloudflare edge IP; otherwise the direct RemoteIpAddress is used, even if a CF-Connecting-IP
    /// header is present (a spoofed header from a non-Cloudflare peer is simply ignored).
    /// </summary>
    public interface INetworkTierResolver
    {
        /// <summary>The real client IP for this request - RemoteIpAddress, or CF-Connecting-IP only when RemoteIpAddress is a genuine Cloudflare edge address.</summary>
        string ResolveClientIp(HttpContext context);

        /// <summary>Which tier this specific request arrived via, based on the resolved client IP.</summary>
        NetworkTier ResolveTier(HttpContext context);
    }

    public class NetworkTierResolver : INetworkTierResolver
    {
        // Cloudflare's published IPv4 edge ranges (https://www.cloudflare.com/ips-v4) - hardcoded
        // rather than fetched at runtime so tier resolution never depends on an external network
        // call succeeding. Update this list if Cloudflare publishes a change.
        private static readonly (IPAddress Network, int PrefixLength)[] CloudflareIpv4Ranges =
        [
            (IPAddress.Parse("173.245.48.0"), 20),
            (IPAddress.Parse("103.21.244.0"), 22),
            (IPAddress.Parse("103.22.200.0"), 22),
            (IPAddress.Parse("103.31.4.0"), 22),
            (IPAddress.Parse("141.101.64.0"), 18),
            (IPAddress.Parse("108.162.192.0"), 18),
            (IPAddress.Parse("190.93.240.0"), 20),
            (IPAddress.Parse("188.114.96.0"), 20),
            (IPAddress.Parse("197.234.240.0"), 22),
            (IPAddress.Parse("198.41.128.0"), 17),
            (IPAddress.Parse("162.158.0.0"), 15),
            (IPAddress.Parse("104.16.0.0"), 13),
            (IPAddress.Parse("104.24.0.0"), 14),
            (IPAddress.Parse("172.64.0.0"), 13),
            (IPAddress.Parse("131.0.72.0"), 22)
        ];

        public string ResolveClientIp(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp != null && IsCloudflareEdgeIp(remoteIp) &&
                context.Request.Headers.TryGetValue("CF-Connecting-IP", out var headerValue) &&
                IPAddress.TryParse(headerValue.ToString(), out var realClientIp))
            {
                return realClientIp.ToString();
            }

            // Never trust CF-Connecting-IP from a peer that isn't actually Cloudflare's edge - fall
            // back to the direct connection's remote IP unconditionally in every other case.
            return remoteIp?.ToString() ?? "unknown";
        }

        public NetworkTier ResolveTier(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp != null && IsCloudflareEdgeIp(remoteIp))
            {
                // Only reachable via the tunnel in the first place if Internet tier is enabled;
                // arriving through Cloudflare's edge at all means Internet tier, regardless of what
                // the real client IP behind CF-Connecting-IP turns out to be.
                return NetworkTier.Internet;
            }

            if (remoteIp == null)
            {
                return NetworkTier.Network;
            }

            if (IPAddress.IsLoopback(remoteIp))
            {
                return NetworkTier.Local;
            }

            var mapped = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;
            if (IPAddress.IsLoopback(mapped))
            {
                return NetworkTier.Local;
            }

            return NetworkTier.Network;
        }

        private static bool IsCloudflareEdgeIp(IPAddress address)
        {
            var mapped = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            if (mapped.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            var addressBytes = mapped.GetAddressBytes();
            var addressInt = (uint)(addressBytes[0] << 24 | addressBytes[1] << 16 | addressBytes[2] << 8 | addressBytes[3]);

            foreach (var (network, prefixLength) in CloudflareIpv4Ranges)
            {
                var networkBytes = network.GetAddressBytes();
                var networkInt = (uint)(networkBytes[0] << 24 | networkBytes[1] << 16 | networkBytes[2] << 8 | networkBytes[3]);
                var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
                if ((addressInt & mask) == (networkInt & mask))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
