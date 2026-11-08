using System.Net;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Service for checking if an IP address is within any manually-banned range.
    /// Loads active ranges from the database. Each range is a CIDR block (e.g. "192.168.0.0/24")
    /// or a single address with no prefix (e.g. "203.0.113.50"), parsed by IPNetwork2.
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

        private bool IsIpInRange(IPAddress ipAddress, string range)
        {
            try
            {
                // A bare address is a single-host range: give it a full-length prefix before parsing.
                string cidr = range.Contains('/') ? range : range + (range.Contains(':') ? "/128" : "/32");
                if (!IPNetwork2.TryParse(cidr, out IPNetwork2? network))
                {
                    _logger.LogDebug("Invalid banned range format");
                    return false;
                }

                return network.Contains(ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error checking banned range: {CidrRange}: {Message} ({ErrorType})", range, ex.Message, ex.GetType().Name);
                return false;
            }
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
