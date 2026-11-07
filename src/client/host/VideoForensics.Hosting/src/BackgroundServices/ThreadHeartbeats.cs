using System.Collections.Concurrent;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// The state of one background service, as the health endpoint reports it. <see cref="LastError"/> is a fixed category
    /// per service, never exception text, because the endpoint is anonymous.
    /// </summary>
    public sealed record ServiceStatus(string Name, string State, DateTimeOffset? LastRunUtc, string? LastError, int FailedItems);

    /// <summary>
    /// Last-tick times and run outcomes for the long-running background loops. Each loop calls <see cref="Beat"/> on every
    /// iteration and <see cref="RecordRun"/> or <see cref="RecordError"/> when it finishes a run. The watchdog reads the beats
    /// back to find loops that have stopped, and the health endpoint reads <see cref="Snapshot"/>. Thread-safe, because loops
    /// and the readers run concurrently.
    /// </summary>
    public sealed class ThreadHeartbeats
    {
        private readonly ConcurrentDictionary<string, (DateTimeOffset Last, TimeSpan Expected)> _beats = new(StringComparer.Ordinal);
        private readonly object _runLock = new();
        private readonly Dictionary<string, (DateTimeOffset? LastRun, string? LastError)> _runs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _failedItems = new(StringComparer.Ordinal);

        /// <summary>
        /// Records that the named loop is alive at <paramref name="now"/>. <paramref name="expectedInterval"/> is how often the loop
        /// normally ticks. A loop that ticks slowly is not stale until it has missed about two of its own intervals.
        /// </summary>
        public void Beat(string name, DateTimeOffset now, TimeSpan? expectedInterval = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _beats[name] = (now, expectedInterval ?? TimeSpan.Zero);
        }

        /// <summary>Makes the named service known before it runs, so the health endpoint lists it as <c>NotStarted</c>.</summary>
        public void Register(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            lock (_runLock)
            {
                _ = _runs.TryAdd(name, (null, null));
            }
        }

        /// <summary>Records a successful run at <paramref name="now"/>. Clears the last error.</summary>
        public void RecordRun(string name, DateTimeOffset now)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            lock (_runLock)
            {
                _runs[name] = (now, null);
            }
        }

        /// <summary>
        /// Records how many items (for the scheduler, accounts) failed in the last tick. Reported as a count next to the state, so
        /// one failing item does not turn the whole service into a failure. Zero clears it.
        /// </summary>
        public void RecordFailedItems(string name, int count)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            lock (_runLock)
            {
                _failedItems[name] = count;
            }
        }

        /// <summary>Records a failed run. <paramref name="category"/> is a fixed label such as "sync failed", never exception text.</summary>
        public void RecordError(string name, DateTimeOffset now, string category)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
            lock (_runLock)
            {
                (DateTimeOffset? lastRun, _) = _runs.TryGetValue(name, out (DateTimeOffset? LastRun, string? LastError) previous) ? previous : (null, null);
                _runs[name] = (lastRun, category);
            }
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

        /// <summary>
        /// Every known service with its state at <paramref name="now"/>, in name order. A service with a last error is
        /// <c>Failed</c>; one that has stopped beating is <c>Stale</c>; one that has beaten or run is <c>Running</c>; one that is
        /// only registered is <c>NotStarted</c>.
        /// </summary>
        public IReadOnlyList<ServiceStatus> Snapshot(DateTimeOffset now, TimeSpan staleAfter)
        {
            List<(string Name, DateTimeOffset? LastRun, string? LastError)> runs;
            Dictionary<string, int> failed;
            lock (_runLock)
            {
                runs = _runs.Select(pair => (pair.Key, pair.Value.LastRun, pair.Value.LastError)).ToList();
                failed = new Dictionary<string, int>(_failedItems, StringComparer.Ordinal);
            }

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach ((string name, _, _) in runs)
            {
                _ = names.Add(name);
            }

            foreach (string name in _beats.Keys)
            {
                _ = names.Add(name);
            }

            var list = new List<ServiceStatus>(names.Count);
            foreach (string name in names)
            {
                (DateTimeOffset? lastRun, string? lastError) = runs.Where(r => r.Name == name).Select(r => (r.LastRun, r.LastError)).FirstOrDefault();
                bool beat = _beats.TryGetValue(name, out (DateTimeOffset Last, TimeSpan Expected) entry);

                string state;
                if (lastError is not null)
                {
                    state = "Failed";
                }
                else if (beat && now - entry.Last > Threshold(entry.Expected, staleAfter))
                {
                    state = "Stale";
                }
                else if (beat || lastRun is not null)
                {
                    state = "Running";
                }
                else
                {
                    state = "NotStarted";
                }

                int failedItems = failed.TryGetValue(name, out int count) ? count : 0;
                list.Add(new ServiceStatus(name, state, lastRun, lastError, failedItems));
            }

            return list;
        }

        private static TimeSpan Threshold(TimeSpan expected, TimeSpan staleAfter)
        {
            TimeSpan twiceExpected = expected * 2;
            return twiceExpected > staleAfter ? twiceExpected : staleAfter;
        }
    }
}
