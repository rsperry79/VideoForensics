using System.Net;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

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
            IReadOnlyList<BannedIpRange> bannedRanges = await _repository.GetActiveAsync(ct);

            foreach (BannedIpRange range in bannedRanges)
            {
                if (IsIpInCidrRange(ipAddress, range.CidrRange))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsIpInCidrRange(IPAddress ipAddress, string cidrRange)
        {
            try
            {
                // Parse CIDR notation: e.g., "192.168.0.0/24"
                string[] parts = cidrRange.Split('/');
                if (parts.Length != 2)
                {
                    _logger.LogDebug("Invalid CIDR format in banned range");
                    return false;
                }

                if (!IPAddress.TryParse(parts[0], out IPAddress? network))
                {
                    _logger.LogDebug("Failed to parse network address in CIDR");
                    return false;
                }

                if (!int.TryParse(parts[1], out int prefixLength) || prefixLength < 0 || prefixLength > (network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
                {
                    _logger.LogDebug("Invalid prefix length in CIDR");
                    return false;
                }

                return IsIpInNetwork(ipAddress, network, prefixLength);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking CIDR range: {CidrRange}", cidrRange);
                return false;
            }
        }

        private bool IsIpInNetwork(IPAddress ipAddress, IPAddress network, int prefixLength)
        {
            if (ipAddress.AddressFamily != network.AddressFamily)
            {
                return false;
            }

            byte[] ipBytes = ipAddress.GetAddressBytes();
            byte[] networkBytes = network.GetAddressBytes();

            int byteCount = prefixLength / 8;
            int bitCount = prefixLength % 8;

            for (int i = 0; i < byteCount; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                {
                    return false;
                }
            }

            if (bitCount > 0)
            {
                byte mask = (byte)(0xFF << (8 - bitCount));
                return (ipBytes[byteCount] & mask) == (networkBytes[byteCount] & mask);
            }

            return true;
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
