using System.Net;
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
                if (!System.Net.IPNetwork.IPAddressCidr.TryParse(range.CidrRange, out var network))
                {
                    _logger.LogWarning("Failed to parse CIDR range '{CidrRange}' (id: {RangeId})", range.CidrRange, range.Id);
                    continue;
                }

                // Check if the given IP is within this range
                if (network.Contains(ipAddress))
                {
                    return true;
                }
            }

            return false;
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
