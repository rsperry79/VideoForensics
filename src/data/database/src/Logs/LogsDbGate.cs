using Microsoft.EntityFrameworkCore;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Logs
{
    /// <summary>
    /// Gives the logs repositories their database context. Reads open a context with no lock. Writes take a single
    /// writer lock first, so logs writes never overlap, the same way the main database's writes are serialized.
    /// </summary>
    public interface ILogsDbGate
    {
        /// <summary>Opens a context for a read. Reads are not gated.</summary>
        Task<VideoForensicsLogsDbContext> OpenReadAsync(CancellationToken ct);

        /// <summary>Waits for the writer lock, then opens a context. Dispose the lease to release the lock.</summary>
        Task<LogsWriteLease> AcquireWriteAsync(CancellationToken ct);
    }

    /// <summary>A writer's context. Holds the logs writer lock until it is disposed.</summary>
    public sealed class LogsWriteLease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _writerLock;

        internal LogsWriteLease(SemaphoreSlim writerLock, VideoForensicsLogsDbContext db)
        {
            _writerLock = writerLock;
            Db = db;
        }

        public VideoForensicsLogsDbContext Db { get; }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _ = _writerLock.Release();
        }
    }

    /// <inheritdoc />
    public sealed class LogsDbGate : ILogsDbGate
    {
        /// <summary>How long a logs write waits for the writer lock before it fails.</summary>
        public static readonly TimeSpan DefaultQueueTimeout = TimeSpan.FromSeconds(3);

        private readonly IDbContextFactory<VideoForensicsLogsDbContext> _factory;
        private readonly SemaphoreSlim _writerLock = new(1, 1);
        private readonly TimeSpan _queueTimeout;

        public LogsDbGate(IDbContextFactory<VideoForensicsLogsDbContext> factory, TimeSpan? queueTimeout = null)
        {
            _factory = factory;
            _queueTimeout = queueTimeout ?? DefaultQueueTimeout;
        }

        /// <inheritdoc />
        public Task<VideoForensicsLogsDbContext> OpenReadAsync(CancellationToken ct) =>
            _factory.CreateDbContextAsync(ct);

        /// <inheritdoc />
        public async Task<LogsWriteLease> AcquireWriteAsync(CancellationToken ct)
        {
            bool entered = await _writerLock.WaitAsync(_queueTimeout, ct);
            if (!entered)
            {
                throw new TimeoutException("A logs write waited too long for the writer lock.");
            }

            try
            {
                VideoForensicsLogsDbContext db = await _factory.CreateDbContextAsync(ct);
                return new LogsWriteLease(_writerLock, db);
            }
            catch
            {
                _ = _writerLock.Release();
                throw;
            }
        }
    }
}
