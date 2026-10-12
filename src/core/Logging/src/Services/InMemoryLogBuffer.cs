using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

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
        private Subscriber[] _subscribers = Array.Empty<Subscriber>();

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
        /// Gets the number of live subscribers currently registered.
        /// </summary>
        public int SubscriberCount => Volatile.Read(ref _subscribers).Length;

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

                // TryWrite on a DropOldest channel never blocks, so publishing under the lock keeps live
                // delivery in sequence order without letting a slow reader stall writers.
                foreach (var subscriber in _subscribers)
                {
                    subscriber.TryPublish(finalEntry);
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
            lock (_syncRoot)
            {
                return SnapshotLocked(minLevel, search, afterSequence, limit);
            }
        }

        private List<LogRecord> SnapshotLocked(string? minLevel, string? search, long? afterSequence, int limit)
        {
            var filtered = new List<LogRecord>();

            foreach (var entry in _entries)
            {
                if (Matches(entry, minLevel, search, afterSequence))
                    filtered.Add(entry);
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
        /// Subscribes to records appended after this call. Unsubscribes when the enumeration is disposed or
        /// <paramref name="ct"/> is cancelled. Slow subscribers lose their oldest unread records instead of blocking writers.
        /// </summary>
        public IAsyncEnumerable<LogRecord> Subscribe(string? minLevel, string? search, long? afterSequence, CancellationToken ct)
        {
            lock (_syncRoot)
            {
                return RegisterLocked(minLevel, search, afterSequence, ct);
            }
        }

        /// <summary>
        /// Atomically takes the backlog snapshot and registers a live subscriber so no record is lost or duplicated
        /// between them: every live record has a sequence greater than the last backlog record.
        /// With no <paramref name="afterSequence"/> the backlog is the newest <paramref name="backlogLimit"/> matches;
        /// with one, the backlog is every newer match (the limit is ignored, otherwise the skipped middle would never be delivered).
        /// </summary>
        public (IReadOnlyList<LogRecord> Backlog, IAsyncEnumerable<LogRecord> Live) SubscribeWithBacklog(
            string? minLevel, string? search, long? afterSequence, int backlogLimit, CancellationToken ct)
        {
            lock (_syncRoot)
            {
                var backlog = SnapshotLocked(minLevel, search, afterSequence, afterSequence.HasValue ? int.MaxValue : backlogLimit);
                var live = RegisterLocked(minLevel, search, _nextSequence - 1, ct);
                return (backlog, live);
            }
        }

        /// <summary>
        /// Disposes the buffer, preventing new log appends and completing all subscribers.
        /// </summary>
        public void Dispose()
        {
            Subscriber[] toComplete;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                toComplete = _subscribers;
                _subscribers = Array.Empty<Subscriber>();
            }

            foreach (var subscriber in toComplete)
            {
                subscriber.Complete();
            }
        }

        private IAsyncEnumerable<LogRecord> RegisterLocked(string? minLevel, string? search, long? afterSequence, CancellationToken ct)
        {
            var subscriber = new Subscriber(minLevel, search, afterSequence);
            if (_disposed || ct.IsCancellationRequested)
            {
                subscriber.Complete();
                return subscriber.ReadAsync(static _ => { }, ct);
            }

            _subscribers = [.. _subscribers, subscriber];
            subscriber.CancellationRegistration = ct.Register(() => Unsubscribe(subscriber));
            return subscriber.ReadAsync(Unsubscribe, ct);
        }

        private void Unsubscribe(Subscriber subscriber)
        {
            lock (_syncRoot)
            {
                _subscribers = Array.FindAll(_subscribers, s => !ReferenceEquals(s, subscriber));
            }

            subscriber.Complete();
        }

        private sealed class Subscriber
        {
            private const int ChannelCapacity = 1000;

            private readonly Channel<LogRecord> _channel = Channel.CreateBounded<LogRecord>(
                new BoundedChannelOptions(ChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });

            private readonly string? _minLevel;
            private readonly string? _search;
            private readonly long? _afterSequence;

            public Subscriber(string? minLevel, string? search, long? afterSequence)
            {
                _minLevel = minLevel;
                _search = search;
                _afterSequence = afterSequence;
            }

            public CancellationTokenRegistration CancellationRegistration { get; set; }

            public void TryPublish(LogRecord record)
            {
                if (Matches(record, _minLevel, _search, _afterSequence))
                {
                    _ = _channel.Writer.TryWrite(record);
                }
            }

            public void Complete()
            {
                _ = _channel.Writer.TryComplete();
                CancellationRegistration.Dispose();
            }

            public async IAsyncEnumerable<LogRecord> ReadAsync(
                Action<Subscriber> onFinished,
                [EnumeratorCancellation] CancellationToken ct)
            {
                try
                {
                    await foreach (var record in _channel.Reader.ReadAllAsync(ct))
                    {
                        yield return record;
                    }
                }
                finally
                {
                    onFinished(this);
                }
            }
        }

        private static bool Matches(LogRecord entry, string? minLevel, string? search, long? afterSequence)
        {
            if (afterSequence.HasValue && entry.Sequence <= afterSequence.Value)
                return false;

            if (minLevel != null && !PassesMinLevelFilter(entry.Level, minLevel))
                return false;

            return search == null || PassesSearchFilter(entry, search);
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
