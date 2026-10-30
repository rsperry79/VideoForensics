using System.Collections.Concurrent;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Last-tick times for the long-running background loops. Each loop calls <see cref="Beat"/> on every iteration, and the
    /// watchdog reads them back to find loops that have stopped. Thread-safe, because loops and the watchdog run concurrently.
    /// </summary>
    public sealed class ThreadHeartbeats
    {
        private readonly ConcurrentDictionary<string, (DateTimeOffset Last, TimeSpan Expected)> _beats = new(StringComparer.Ordinal);

        /// <summary>
        /// Records that the named loop is alive at <paramref name="now"/>. <paramref name="expectedInterval"/> is how often the loop
        /// normally ticks. A loop that ticks slowly is not stale until it has missed about two of its own intervals.
        /// </summary>
        public void Beat(string name, DateTimeOffset now, TimeSpan? expectedInterval = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _beats[name] = (now, expectedInterval ?? TimeSpan.Zero);
        }

        /// <summary>
        /// Names of loops that have not beaten for longer than their threshold, in name order. The threshold is
        /// <paramref name="staleAfter"/>, or twice the loop's expected interval when that is longer. A loop that has never
        /// beaten is not reported, because it may not have started yet.
        /// </summary>
        public IReadOnlyList<string> StaleServices(DateTimeOffset now, TimeSpan staleAfter)
        {
            return _beats
                .Where(pair => now - pair.Value.Last > Threshold(pair.Value.Expected, staleAfter))
                .Select(pair => pair.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static TimeSpan Threshold(TimeSpan expected, TimeSpan staleAfter)
        {
            TimeSpan twiceExpected = expected * 2;
            return twiceExpected > staleAfter ? twiceExpected : staleAfter;
        }
    }
}
