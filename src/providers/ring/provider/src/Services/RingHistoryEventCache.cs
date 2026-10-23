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
    /// Entries are kept per Ring account: one cache instance may serve several accounts, and
    /// account B must never be served account A's events.
    /// </summary>
    public interface IRingHistoryEventCache
    {
        /// <summary>
        /// Gets the cached doorbot history events for the specified account and date range.
        /// Cache widening logic: a request whose [startDate, endDate] falls entirely inside
        /// what's already cached for that account is served by filtering the cached events in memory with no
        /// network call. A request that needs data older than what's cached (e.g. a device with
        /// an earlier watermark) fetches the union of the two ranges and widens the cache.
        /// A session with no account identity is fetched directly and never cached.
        /// </summary>
        Task<List<DoorbotHistoryEvent>> GetEventsAsync(Session session, DateTime startDate, DateTime endDate, CancellationToken ct);

        /// <summary>
        /// Best-effort hint for whether events in the given date range are already cached for this account.
        /// No lock is held; this is a hint for callers deciding whether to skip an inter-device delay,
        /// not a correctness-critical read.
        /// </summary>
        bool IsCached(Session session, DateTime startDate, DateTime endDate);
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
        private const string CacheKeyPrefix = "ring_doorbot_history:";
        private const int CacheTtlHours = 24;

        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        // Serializes the cache-miss path (decide range, fetch, store) so each fetch widens from the
        // entry the previous fetch stored; otherwise a slower, narrower fetch could overwrite a wider one.
        private readonly SemaphoreSlim _fetchGate = new(1, 1);

        public RingHistoryEventCache(IMemoryCache cache, ILogger logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// The cache key for one account. The key is built from the login name, never from a shared constant, so two
        /// accounts can never read each other's entries. Null when the session has no account identity.
        /// </summary>
        private static string? KeyFor(Session session)
        {
            string? username = session.Username;
            return string.IsNullOrWhiteSpace(username) ? null : CacheKeyPrefix + username.Trim().ToUpperInvariant();
        }

        public bool IsCached(Session session, DateTime startDate, DateTime endDate)
        {
            // No lock: this is a best-effort hint for a caller deciding whether to skip an inter-
            // device delay, not a correctness-critical read - a stale answer just means one call
            // waits (or doesn't) a few seconds longer/shorter than strictly necessary.
            string? key = KeyFor(session);
            return key is not null && _cache.TryGetValue(key, out CachedHistoryRange? cachedRange) && cachedRange != null &&
                       startDate >= cachedRange.Start &&
                       endDate <= cachedRange.End;
        }

        private List<DoorbotHistoryEvent>? TryServeFromCache(string key, DateTime startDate, DateTime endDate, out CachedHistoryRange? cachedRange)
        {
            if (_cache.TryGetValue(key, out cachedRange))
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

            return null;
        }

        public async Task<List<DoorbotHistoryEvent>> GetEventsAsync(Session session, DateTime startDate, DateTime endDate, CancellationToken ct)
        {
            string? key = KeyFor(session);
            if (key is null)
            {
                // No account identity to key on: sharing an entry could leak another account's events, so do not cache.
                _logger.LogWarning("Doorbot history requested for a session with no account identity; fetching without caching");
                return await session.GetDoorbotsHistory(startDate, endDate) ?? [];
            }

            List<DoorbotHistoryEvent>? served = TryServeFromCache(key, startDate, endDate, out _);
            if (served != null)
            {
                return served;
            }

            await _fetchGate.WaitAsync(ct);
            try
            {
                // Re-read inside the gate: another caller may have stored a range that now covers this request.
                served = TryServeFromCache(key, startDate, endDate, out CachedHistoryRange? cachedRange);
                if (served != null)
                {
                    return served;
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

                _ = _cache.Set(key, cacheEntry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheTtlHours)
                });

                // Return only the slice matching what was actually requested
                return fetchStart == startDate && fetchEnd == endDate
                    ? cacheEntry.Events
                    : cacheEntry.Events
                    .Where(e => e.CreatedAtDateTime.HasValue &&
                               e.CreatedAtDateTime.Value >= startDate &&
                               e.CreatedAtDateTime.Value <= endDate)
                    .ToList();
            }
            finally
            {
                _ = _fetchGate.Release();
            }
        }
    }
}
