using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Service for checking if an IP address is within any manually-banned CIDR range.
    /// Loads active ranges from the database and parses CIDR notation to check containment.
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
            // Get all active banned IP ranges
            var bannedRanges = await _repository.GetActiveAsync(ct);

            foreach (var range in bannedRanges)
            {
                // Try to parse the CIDR range; skip malformed entries and continue checking others
                if (!TryParseCidr(range.CidrRange, out IPAddress? networkAddress, out int prefixLength))
                {
                    _logger.LogWarning("Failed to parse CIDR range '{CidrRange}' (id: {RangeId})", range.CidrRange, range.Id);
                    continue;
                }

                // Check if the given IP is within this range
                if (IsIpInRange(ipAddress, networkAddress!, prefixLength))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses a CIDR notation string into network address and prefix length.
        /// </summary>
        private static bool TryParseCidr(string cidr, out IPAddress? networkAddress, out int prefixLength)
        {
            networkAddress = null;
            prefixLength = 0;

            try
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2)
                {
                    return false;
                }

                if (!IPAddress.TryParse(parts[0], out networkAddress) || networkAddress == null)
                {
                    return false;
                }

                if (!int.TryParse(parts[1], out prefixLength))
                {
                    return false;
                }

                // Validate prefix length based on address family
                int maxPrefix = networkAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                if (prefixLength < 0 || prefixLength > maxPrefix)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an IP address is contained within a CIDR range.
        /// </summary>
        private static bool IsIpInRange(IPAddress ipAddress, IPAddress networkAddress, int prefixLength)
        {
            // Ensure both addresses are the same family
            if (ipAddress.AddressFamily != networkAddress.AddressFamily)
            {
                return false;
            }

            var ipBytes = ipAddress.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            // Calculate the number of complete bytes to compare
            int completeBytesToCheck = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            // Check complete bytes
            for (int i = 0; i < completeBytesToCheck; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                {
                    return false;
                }
            }

            // Check remaining bits in the next byte (if any)
            if (remainingBits > 0 && completeBytesToCheck < ipBytes.Length)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((ipBytes[completeBytesToCheck] & mask) != (networkBytes[completeBytesToCheck] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Null logger implementation to avoid null reference checks.
    /// </summary>
    internal class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
