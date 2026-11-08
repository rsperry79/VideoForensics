namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Unit of work for executing multi-entity operations atomically within a single transaction.</summary>
    public interface IUnitOfWork
    {
        /// <summary>Executes a work function within a shared transaction context, committing on success or rolling back on exception.</summary>
        Task<T> ExecuteAsync<T>(Func<IUnitOfWorkContext, Task<T>> work, CancellationToken ct);

        /// <summary>
        /// Runs a read-only function on its own context, with no transaction and no write gate, so a read never waits behind
        /// a write. Use only for reads: nothing the function changes is saved.
        /// </summary>
        Task<T> ReadAsync<T>(Func<IUnitOfWorkContext, Task<T>> read, CancellationToken ct);
    }
}
