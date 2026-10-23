using System.Collections.Concurrent;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Last-tick times for the long-running background loops. Each loop calls <see cref="Beat"/> on every iteration, and the
    /// watchdog reads them back to find loops that have stopped. Thread-safe, because loops and the watchdog run concurrently.
    /// </summary>
    public sealed class ThreadHeartbeats
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastBeat = new(StringComparer.Ordinal);

        /// <summary>Records that the named loop is alive at <paramref name="now"/>.</summary>
        public void Beat(string name, DateTimeOffset now)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _lastBeat[name] = now;
        }

        /// <summary>
        /// Names of loops whose last beat is older than <paramref name="staleAfter"/>, in name order. A loop that has never
        /// beaten is not reported, because it may not have started yet.
        /// </summary>
        public IReadOnlyList<string> StaleServices(DateTimeOffset now, TimeSpan staleAfter)
        {
            return _lastBeat
                .Where(pair => now - pair.Value > staleAfter)
                .Select(pair => pair.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
    }
}
