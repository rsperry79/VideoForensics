using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Process-wide sync console. Each line is kept in two places: a pending queue that the hub broadcaster drains, and a
    /// bounded history that pages read by position, so several readers each see every line. Every line is also written to
    /// the server log, which is what the log viewer shows. Both stores are bounded so an unread sync cannot grow memory without limit.
    /// </summary>
    public sealed class SyncActivityLog : ISyncActivityLog
    {
        /// <summary>Maximum lines held between drains.</summary>
        public const int MaxPendingLines = 500;

        /// <summary>Maximum lines retained for readers that open a page mid-sync.</summary>
        public const int MaxRetainedLines = 500;

        private readonly ConcurrentQueue<string> _pending = new();
        private readonly ILogger<SyncActivityLog> _logger;
        private readonly object _gate = new();
        private readonly List<string> _retained = new();
        private long _appended;

        public SyncActivityLog(ILogger<SyncActivityLog> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public void Append(string line)
        {
            _logger.LogInformation("Sync: {SyncLine}", line);
            Add(line);
        }

        /// <inheritdoc />
        public void AppendError(string line)
        {
            _logger.LogError("Sync: {SyncLine}", line);
            Add(line);
        }

        private void Add(string line)
        {
            _pending.Enqueue(line);
            while (_pending.Count > MaxPendingLines && _pending.TryDequeue(out _))
            {
            }

            lock (_gate)
            {
                _retained.Add(line);
                _appended++;
                if (_retained.Count > MaxRetainedLines)
                {
                    _retained.RemoveAt(0);
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> Drain()
        {
            var drained = new List<string>();
            while (_pending.TryDequeue(out string? line))
            {
                drained.Add(line);
            }

            return drained;
        }

        /// <inheritdoc />
        public long Position
        {
            get
            {
                lock (_gate)
                {
                    return _appended;
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ReadSince(long position, out long nextPosition)
        {
            lock (_gate)
            {
                long firstRetained = _appended - _retained.Count;
                int skip = (int)(Math.Max(position, firstRetained) - firstRetained);
                nextPosition = _appended;
                return _retained.Skip(skip).ToList();
            }
        }
    }
}
