using System.Collections.Concurrent;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Process-wide queue of sync console lines. Bounded so a sync that is never drained (no client connected) cannot
    /// grow memory without limit; the oldest lines are dropped first.
    /// </summary>
    public sealed class SyncActivityLog : ISyncActivityLog
    {
        /// <summary>Maximum lines held between drains.</summary>
        public const int MaxPendingLines = 500;

        private readonly ConcurrentQueue<string> _lines = new();

        /// <inheritdoc />
        public void Append(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > MaxPendingLines && _lines.TryDequeue(out _))
            {
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> Drain()
        {
            var drained = new List<string>();
            while (_lines.TryDequeue(out string? line))
            {
                drained.Add(line);
            }

            return drained;
        }
    }
}
