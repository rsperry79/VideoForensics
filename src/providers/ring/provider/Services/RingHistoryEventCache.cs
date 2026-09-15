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

    public class RingHistoryEventCache : IRingHistoryEventCache
    {
        private readonly ILogger _logger;

        // GetDoorbotsHistory returns the FULL account history (all devices), not just one device's.
        // Cache it so a sequential per-device download loop doesn't re-fetch the same data N times.
        private readonly SemaphoreSlim _historyCacheLock = new(1, 1);
        private DateTime? _cachedHistoryStart;
        private DateTime? _cachedHistoryEnd;
        private List<DoorbotHistoryEvent>? _cachedHistoryEvents;

        public RingHistoryEventCache(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsCached(DateTime startDate, DateTime endDate)
        {
            // No lock: this is a best-effort hint for a caller deciding whether to skip an inter-
            // device delay, not a correctness-critical read - a stale answer just means one call
            // waits (or doesn't) a few seconds longer/shorter than strictly necessary.
            return _cachedHistoryEvents != null && _cachedHistoryStart.HasValue && _cachedHistoryEnd.HasValue &&
                   startDate >= _cachedHistoryStart.Value && endDate <= _cachedHistoryEnd.Value;
        }

        public async Task<List<DoorbotHistoryEvent>> GetEventsAsync(Session session, DateTime startDate, DateTime endDate, CancellationToken ct)
        {
            await _historyCacheLock.WaitAsync(ct);
            try
            {
                if (_cachedHistoryEvents != null && _cachedHistoryStart.HasValue && _cachedHistoryEnd.HasValue &&
                    startDate >= _cachedHistoryStart.Value && endDate <= _cachedHistoryEnd.Value)
                {
                    if (startDate == _cachedHistoryStart.Value && endDate == _cachedHistoryEnd.Value)
                    {
                        _logger.LogInformation("Reusing cached doorbot history for {StartDate} to {EndDate} ({Count} events)",
                            startDate, endDate, _cachedHistoryEvents.Count);
                        return _cachedHistoryEvents;
                    }

                    var filtered = _cachedHistoryEvents
                        .Where(e => e.CreatedAtDateTime.HasValue && e.CreatedAtDateTime.Value >= startDate && e.CreatedAtDateTime.Value <= endDate)
                        .ToList();
                    _logger.LogInformation("Serving {StartDate} to {EndDate} ({Count} events) from the wider cached range {CachedStart} to {CachedEnd} - no fetch needed",
                        startDate, endDate, filtered.Count, _cachedHistoryStart, _cachedHistoryEnd);
                    return filtered;
                }

                DateTime fetchStart = _cachedHistoryStart.HasValue && _cachedHistoryStart.Value < startDate ? _cachedHistoryStart.Value : startDate;
                DateTime fetchEnd = _cachedHistoryEnd.HasValue && _cachedHistoryEnd.Value > endDate ? _cachedHistoryEnd.Value : endDate;

                _logger.LogInformation("Fetching doorbot history for {StartDate} to {EndDate}", fetchStart, fetchEnd);
                List<DoorbotHistoryEvent> events = await session.GetDoorbotsHistory(fetchStart, fetchEnd);
                _cachedHistoryEvents = events ?? [];
                _cachedHistoryStart = fetchStart;
                _cachedHistoryEnd = fetchEnd;

                // The fetch above may have covered a wider union range than this specific caller
                // asked for (to satisfy the cache widening above) - callers don't re-filter by date
                // themselves, so return only the slice matching what was actually requested.
                return fetchStart == startDate && fetchEnd == endDate
                    ? _cachedHistoryEvents
                    : _cachedHistoryEvents
                    .Where(e => e.CreatedAtDateTime.HasValue && e.CreatedAtDateTime.Value >= startDate && e.CreatedAtDateTime.Value <= endDate)
                    .ToList();
            }
            finally
            {
                _ = _historyCacheLock.Release();
            }
        }
    }
}
