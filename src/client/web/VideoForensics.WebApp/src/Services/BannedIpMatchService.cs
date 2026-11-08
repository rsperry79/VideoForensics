using System.Net;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Service for checking if an IP address is within any manually-banned range.
    /// Loads active ranges from the database. Each range is a CIDR block (e.g. "192.168.0.0/24")
    /// or a single address with no prefix (e.g. "203.0.113.50"), parsed by IPNetwork (pinned to 2.1.2, the version Makaretu needs).
    /// </summary>
    public class BannedIpMatchService : IBannedIpMatchService
    {
        private readonly IBannedIpRangeRepository _repository;
        private readonly ILogger<BannedIpMatchService> _logger;

        public BannedIpMatchService(IBannedIpRangeRepository repository, ILogger<BannedIpMatchService>? logger = null)
        {
            _repository = repository;
            _logger = logger ?? new NullLogger<BannedIpMatchService>();
        }

        public async Task<bool> IsIpBannedAsync(IPAddress ipAddress, CancellationToken ct)
        {
            IReadOnlyList<BannedIpRange> bannedRanges = await _repository.GetActiveAsync(ct);

            // An IPv4-mapped IPv6 address (::ffff:a.b.c.d) is the IPv4 address a.b.c.d; match it as IPv4
            // so an IPv4 ban also covers the mapped form the socket layer reports.
            IPAddress candidate = ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4() : ipAddress;

            foreach (BannedIpRange range in bannedRanges)
            {
                if (IsIpInRange(candidate, range.CidrRange))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the address falls inside the CIDR range. A bare address is a single-host range. IPv4-mapped IPv6
        /// addresses match IPv4 ranges. Compares address bytes directly: no IPNetwork type, because Makaretu brings its own
        /// copy of that assembly and the two clash.
        /// </summary>
        private bool IsIpInRange(IPAddress ipAddress, string range)
        {
            string[] parts = range.Split('/');
            if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out IPAddress? network))
            {
                _logger.LogDebug("Invalid banned range format");
                return false;
            }

            IPAddress address = ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4() : ipAddress;
            network = network.IsIPv4MappedToIPv6 ? network.MapToIPv4() : network;

            int maxPrefix = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            int prefix = maxPrefix;
            if (parts.Length == 2 && (!int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > maxPrefix))
            {
                _logger.LogDebug("Invalid banned range prefix");
                return false;
            }

            byte[] a = address.GetAddressBytes();
            byte[] n = network.GetAddressBytes();
            if (a.Length != n.Length)
            {
                return false;
            }

            int wholeBytes = prefix / 8;
            for (int i = 0; i < wholeBytes; i++)
            {
                if (a[i] != n[i])
                {
                    return false;
                }
            }

            int remainingBits = prefix % 8;
            if (remainingBits == 0)
            {
                return true;
            }

            int mask = (0xFF << (8 - remainingBits)) & 0xFF;
            return (a[wholeBytes] & mask) == (n[wholeBytes] & mask);
        }
    }

    /// <summary>
    /// Null logger implementation to avoid null reference checks.
    /// </summary>
    internal class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
