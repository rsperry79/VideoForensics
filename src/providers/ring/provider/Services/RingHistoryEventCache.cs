using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Caches Ring doorbot history events to avoid re-fetching the full account history
    /// multiple times when processing multiple devices sequentially. GetDoorbotsHistory
    /// returns the FULL account history (all devices), not just one device's. A cache
    /// allows a sequential per-device download loop to reuse the data instead of making
    /// redundant API calls.
    /// </summary>
    public interface IRingHistoryEventCache
    {
        /// <summary>
        /// Gets the cached doorbot history events for the specified date range.
        /// Cache widening logic: a request whose [startDate, endDate] falls entirely inside
        /// what's already cached is served by filtering the cached events in memory with no
        /// network call. A request that needs data older than what's cached (e.g. a device with
        /// an earlier watermark) fetches the union of the two ranges and widens the cache.
        /// </summary>
        Task<List<DoorbotHistoryEvent>> GetEventsAsync(Session session, DateTime startDate, DateTime endDate, CancellationToken ct);

        /// <summary>
        /// Best-effort hint for whether events in the given date range are already cached.
        /// No lock is held; this is a hint for callers deciding whether to skip an inter-device delay,
        /// not a correctness-critical read.
        /// </summary>
        bool IsCached(DateTime startDate, DateTime endDate);
    }

    /// <summary>
    /// Composite cache entry that stores both events and metadata together.
    /// This ensures that widening bounds survive cache eviction.
    /// </summary>
    internal class CachedHistoryRange
    {
        public List<DoorbotHistoryEvent> Events { get; set; } = [];
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class RingHistoryEventCache : IRingHistoryEventCache
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private const string CacheKey = "ring_doorbot_history";
        private const int CacheTtlHours = 24;

        public RingHistoryEventCache(IMemoryCache cache, ILogger logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public bool IsCached(DateTime startDate, DateTime endDate)
        {
            // No lock: this is a best-effort hint for a caller deciding whether to skip an inter-
            // device delay, not a correctness-critical read - a stale answer just means one call
            // waits (or doesn't) a few seconds longer/shorter than strictly necessary.
            if (_cache.TryGetValue(CacheKey, out CachedHistoryRange? cachedRange))
            {
                return cachedRange != null &&
                       startDate >= cachedRange.Start &&
                       endDate <= cachedRange.End;
            }

            return false;
        }

        public async Task<List<DoorbotHistoryEvent>> GetEventsAsync(Session session, DateTime startDate, DateTime endDate, CancellationToken ct)
        {
            // Try to get cached range with metadata
            if (_cache.TryGetValue(CacheKey, out CachedHistoryRange? cachedRange))
            {
                if (cachedRange != null &&
                    startDate >= cachedRange.Start &&
                    endDate <= cachedRange.End)
                {
                    // Request is entirely within cached range
                    if (startDate == cachedRange.Start && endDate == cachedRange.End)
                    {
                        _logger.LogInformation("Reusing cached doorbot history for {StartDate} to {EndDate} ({Count} events)",
                            startDate, endDate, cachedRange.Events.Count);
                        return cachedRange.Events;
                    }

                    // Filter to requested range
                    var filtered = cachedRange.Events
                        .Where(e => e.CreatedAtDateTime.HasValue &&
                                   e.CreatedAtDateTime.Value >= startDate &&
                                   e.CreatedAtDateTime.Value <= endDate)
                        .ToList();
                    _logger.LogInformation("Serving {StartDate} to {EndDate} ({Count} events) from the wider cached range {CachedStart} to {CachedEnd} - no fetch needed",
                        startDate, endDate, filtered.Count, cachedRange.Start, cachedRange.End);
                    return filtered;
                }
            }

            // Determine the range to fetch: expand if we need to widen cache
            DateTime fetchStart = startDate;
            DateTime fetchEnd = endDate;

            if (cachedRange != null)
            {
                // Widen boundaries if needed
                if (cachedRange.Start < startDate)
                {
                    fetchStart = cachedRange.Start;
                }

                if (cachedRange.End > endDate)
                {
                    fetchEnd = cachedRange.End;
                }
            }

            _logger.LogInformation("Fetching doorbot history for {StartDate} to {EndDate}", fetchStart, fetchEnd);
            List<DoorbotHistoryEvent> events = await session.GetDoorbotsHistory(fetchStart, fetchEnd);

            // Store composite entry (events + metadata) with long TTL
            var cacheEntry = new CachedHistoryRange
            {
                Events = events ?? [],
                Start = fetchStart,
                End = fetchEnd,
                CachedAt = DateTime.UtcNow
            };

            _cache.Set(CacheKey, cacheEntry, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheTtlHours)
            });

            // Return only the slice matching what was actually requested
            if (fetchStart == startDate && fetchEnd == endDate)
            {
                return cacheEntry.Events;
            }

            return cacheEntry.Events
                .Where(e => e.CreatedAtDateTime.HasValue &&
                           e.CreatedAtDateTime.Value >= startDate &&
                           e.CreatedAtDateTime.Value <= endDate)
                .ToList();
        }
    }
}
