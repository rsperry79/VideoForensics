using System;
using System.Collections.Generic;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// A thread-safe, fixed-capacity in-memory circular buffer for log entries.
    /// Each entry receives a monotonic sequence number (starting at 1).
    /// Supports querying with filters (minLevel, search term, afterSequence, limit).
    /// </summary>
    public sealed class InMemoryLogBuffer : IDisposable
    {
        private const int DefaultCapacity = 5000;

        private readonly int _capacity;
        private readonly List<LogRecord> _entries;
        private readonly object _syncRoot = new();
        private long _nextSequence = 1;
        private bool _disposed;

        /// <summary>
        /// Gets the highest sequence number currently in the buffer.
        /// </summary>
        public long LatestSequence
        {
            get
            {
                lock (_syncRoot)
                {
                    return _nextSequence - 1;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance with the specified capacity (default 5000).
        /// </summary>
        public InMemoryLogBuffer(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

            _capacity = capacity;
            _entries = new List<LogRecord>(capacity);
        }

        /// <summary>
        /// Appends a log record to the buffer with a new monotonic sequence number.
        /// The sequence field in the entry parameter is ignored; a new one is assigned.
        /// Thread-safe; maintains monotonic ordering across concurrent writers.
        /// When buffer exceeds capacity, evicts the oldest entry.
        /// </summary>
        public void Append(ref LogRecord entry)
        {
            if (_disposed)
                return;

            lock (_syncRoot)
            {
                // Assign the next sequence number
                var sequence = _nextSequence++;

                // Create the final entry with the assigned sequence
                var finalEntry = new LogRecord(
                    Sequence: sequence,
                    TimestampUtc: entry.TimestampUtc,
                    Level: entry.Level,
                    Category: entry.Category,
                    Message: entry.Message,
                    Exception: entry.Exception);

                // Add to buffer
                _entries.Add(finalEntry);

                // Evict oldest if over capacity
                if (_entries.Count > _capacity)
                {
                    _entries.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of entries matching the given filters, ordered by sequence ascending.
        /// When limit is exceeded and afterSequence is null, returns the newest (highest sequence) entries.
        /// </summary>
        /// <param name="minLevel">Minimum log level (e.g., "Warning"); null = no filtering</param>
        /// <param name="search">Search term to find in message/category/exception (case-insensitive); null = no filtering</param>
        /// <param name="afterSequence">Only return entries with sequence > this value; null = no afterSequence filter</param>
        /// <param name="limit">Maximum entries to return</param>
        public List<LogRecord> GetSnapshot(string? minLevel, string? search, long? afterSequence, int limit)
        {
            var filtered = new List<LogRecord>();

            lock (_syncRoot)
            {
                foreach (var entry in _entries)
                {
                    // Filter by afterSequence
                    if (afterSequence.HasValue && entry.Sequence <= afterSequence.Value)
                        continue;

                    // Filter by minLevel
                    if (minLevel != null && !PassesMinLevelFilter(entry.Level, minLevel))
                        continue;

                    // Filter by search term
                    if (search != null && !PassesSearchFilter(entry, search))
                        continue;

                    filtered.Add(entry);
                }
            }

            // If limit is exceeded and afterSequence is null, return the newest (highest sequence) entries
            if (filtered.Count > limit && afterSequence == null)
            {
                var startIndex = filtered.Count - limit;
                return filtered.GetRange(startIndex, limit);
            }

            // Otherwise, respect the limit from the start
            if (filtered.Count > limit)
            {
                return filtered.GetRange(0, limit);
            }

            return filtered;
        }

        /// <summary>
        /// Disposes the buffer, preventing new log appends.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_syncRoot)
            {
                _disposed = true;
            }
        }

        private static bool PassesMinLevelFilter(string entryLevel, string minLevel)
        {
            var entryPriority = GetLogLevelPriority(entryLevel);
            var minPriority = GetLogLevelPriority(minLevel);
            return entryPriority >= minPriority;
        }

        private static int GetLogLevelPriority(string level)
        {
            return level.ToLower() switch
            {
                "trace" => 0,
                "debug" => 1,
                "information" => 2,
                "warning" => 3,
                "error" => 4,
                "critical" => 5,
                _ => 2 // Default to Information
            };
        }

        private static bool PassesSearchFilter(LogRecord entry, string search)
        {
            var searchLower = search.ToLower();
            return entry.Message.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(entry.Exception) && entry.Exception.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
        }
    }
}
