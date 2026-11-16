using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Runs a write on a fresh database context with no transaction, under the same write gate as units of work.
    /// Used for statements that SQLite refuses inside a transaction, such as VACUUM, REINDEX and wal_checkpoint.
    /// </summary>
    public interface ITransactionlessWriteGate
    {
        /// <summary>
        /// Runs <paramref name="work"/> on a fresh context with no transaction, under the write gate.
        /// Throws <see cref="InvalidOperationException"/> if called inside an open unit of work, because the statement
        /// would then run inside that unit's transaction.
        /// </summary>
        /// <typeparam name="T">The result type of the work.</typeparam>
        /// <param name="work">The statement to run. It receives a fresh context and the cancellation token.</param>
        /// <param name="ct">Cancellation token for waiting on the gate and creating the context.</param>
        Task<T> ExecuteWithoutTransactionAsync<T>(Func<VideoForensicsDbContext, CancellationToken, Task<T>> work, CancellationToken ct);
    }
}
