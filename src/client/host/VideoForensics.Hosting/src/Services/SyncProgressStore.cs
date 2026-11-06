using VideoForensics.Client.Hosting.Contracts;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Process-wide holder of the latest <see cref="SyncProgress"/>. A lock guards the single reference so a reader never
    /// sees a torn reading, and the record is immutable so the reference can be shared freely.
    /// </summary>
    public sealed class SyncProgressStore : ISyncProgressStore
    {
        private readonly object _gate = new();

        /// <inheritdoc />
        public SyncProgress Current
        {
            get
            {
                lock (_gate)
                {
                    return field;
                }
            }

            private set;
        } = SyncProgress.Idle;

        /// <inheritdoc />
        public void Set(SyncProgress progress)
        {
            lock (_gate)
            {
                Current = progress;
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            Set(SyncProgress.Idle);
        }
    }
}
