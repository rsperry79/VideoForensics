namespace VideoForensics.Data.Database.Sqlite.DependencyInjection
{
    /// <summary>Builds the connection strings used to open VideoForensics SQLite files.</summary>
    public static class SqliteConnectionStrings
    {
        /// <summary>
        /// Connection string for a SQLite file opened in WAL mode.
        /// </summary>
        /// <remarks>
        /// Deliberately omits <c>Cache=Shared</c>. Shared-cache mode enforces table-level locks between connections in the
        /// same process, and a write that hits a table held by another connection fails immediately with SQLITE_LOCKED
        /// (error 6), which ignores the busy timeout. In WAL mode with private caches, readers and the writer run
        /// concurrently, and a writer that does contend waits up to <c>Default Timeout</c> seconds instead of failing.
        /// </remarks>
        /// <param name="dbPath">Full path to the SQLite database file.</param>
        /// <returns>A connection string for <paramref name="dbPath"/>.</returns>
        public static string ForFile(string dbPath)
        {
            return $"Data Source={dbPath};Pooling=true;Default Timeout=5";
        }
    }
}
