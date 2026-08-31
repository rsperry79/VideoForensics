namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Low-level database lifecycle operations that don't fit the entity repositories.</summary>
    public interface IDatabaseMaintenanceService
    {
        /// <summary>
        /// Flushes pending writes and releases every pooled connection to the database file, so
        /// the underlying file (and its -wal/-shm sidecar files) can be safely deleted or replaced
        /// by the caller immediately afterward. Needed before operations like factory reset - the
        /// ADO.NET connection pool keeps OS-level file handles open even after a DbContext using it
        /// is disposed, which otherwise causes a "file is being used by another process" failure.
        /// </summary>
        Task ReleaseAllConnectionsAsync(CancellationToken ct = default);
    }
}
