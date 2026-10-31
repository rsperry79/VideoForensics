using System.Collections.Concurrent;

namespace VideoForensics.Data.Common.Diagnostics
{
    /// <summary>
    /// Transactions that are open right now, with the caller and start time of each. When a background loop stalls, the
    /// watchdog reads this to say what the stalled work was waiting on. Thread-safe: transactions open and close concurrently.
    /// </summary>
    public sealed class InFlightTransactions
    {
        private readonly ConcurrentDictionary<long, InFlightTransaction> _open = new();
        private long _nextId;

        /// <summary>Records a transaction as open. Dispose the returned token when the transaction ends.</summary>
        public IDisposable Begin(string caller, DateTimeOffset startedUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caller);
            long id = Interlocked.Increment(ref _nextId);
            _open[id] = new InFlightTransaction(caller, startedUtc);
            return new Token(this, id);
        }

        /// <summary>The open transactions, oldest first.</summary>
        public IReadOnlyList<InFlightTransaction> Snapshot()
        {
            return _open.Values.OrderBy(t => t.StartedUtc).ToList();
        }

        private void End(long id)
        {
            _ = _open.TryRemove(id, out _);
        }

        /// <summary>One open transaction: who started it and when.</summary>
        public sealed record InFlightTransaction(string Caller, DateTimeOffset StartedUtc);

        private sealed class Token(InFlightTransactions owner, long id) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                // Dispose can run more than once; only the first call removes the entry.
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    owner.End(id);
                }
            }
        }
    }
}
