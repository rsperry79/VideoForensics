namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Database-type specific maintenance. Each database provider implements this (SQLite today). Everything above it goes
    /// through <see cref="IDatabaseAdministrationService"/>, so the Database page never names a provider.
    /// </summary>
    public interface IDatabaseEngine
    {
        /// <summary>Engine name shown on the Database page, e.g. "SQLite".</summary>
        string Name { get; }

        /// <summary>Physical size and layout of the database.</summary>
        Task<DatabaseFileStats> GetFileStatsAsync(CancellationToken ct);

        /// <summary>Rebuilds the file to reclaim free pages.</summary>
        Task CompactAsync(CancellationToken ct);

        /// <summary>Runs the engine's own integrity check. Returns its verdict text, which is "ok" when the file is sound.</summary>
        Task<string> CheckIntegrityAsync(CancellationToken ct);

        /// <summary>Rebuilds every index from the table data.</summary>
        Task RebuildIndexesAsync(CancellationToken ct);
    }

    /// <summary>Physical database layout, as reported by the engine.</summary>
    /// <param name="Engine">Engine name.</param>
    /// <param name="Location">Where the database file lives.</param>
    /// <param name="FileBytes">Size of the main database file.</param>
    /// <param name="PageCount">Total pages in the file.</param>
    /// <param name="FreePageCount">Pages not in use, which compaction can reclaim.</param>
    /// <param name="PageSize">Bytes per page.</param>
    /// <param name="JournalMode">Engine journal mode, such as WAL.</param>
    public sealed record DatabaseFileStats(
        string Engine,
        string Location,
        long FileBytes,
        long PageCount,
        long FreePageCount,
        long PageSize,
        string JournalMode);
}
