using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.Services
{
    /// <summary>
    /// SQLite implementation of <see cref="IDatabaseEngine"/>. Each call opens its own connection, so a VACUUM or REINDEX
    /// never runs inside a transaction the app holds open on its own context.
    /// </summary>
    public sealed class SqliteDatabaseEngine(IDbContextFactory<VideoForensicsDbContext> factory) : IDatabaseEngine
    {
        /// <inheritdoc />
        public string Name => "SQLite";

        /// <inheritdoc />
        public async Task<DatabaseFileStats> GetFileStatsAsync(CancellationToken ct)
        {
            string connectionString = await ConnectionStringAsync(ct);
            string location = new SqliteConnectionStringBuilder(connectionString).DataSource;
            long fileBytes = File.Exists(location) ? new FileInfo(location).Length : 0;

            return new DatabaseFileStats(
                Name,
                location,
                fileBytes,
                await ScalarAsync<long>(connectionString, "SELECT page_count FROM pragma_page_count()", ct),
                await ScalarAsync<long>(connectionString, "SELECT freelist_count FROM pragma_freelist_count()", ct),
                await ScalarAsync<long>(connectionString, "SELECT page_size FROM pragma_page_size()", ct),
                await ScalarAsync<string>(connectionString, "PRAGMA journal_mode", ct));
        }

        /// <inheritdoc />
        public async Task CompactAsync(CancellationToken ct)
        {
            await NonQueryAsync(await ConnectionStringAsync(ct), "VACUUM", ct);
        }

        /// <inheritdoc />
        public async Task<string> CheckIntegrityAsync(CancellationToken ct)
        {
            // integrity_check returns one row per problem, or a single "ok" row when the file is sound.
            List<string> rows = await ColumnAsync(await ConnectionStringAsync(ct), "PRAGMA integrity_check", ct);
            return rows.Count == 1 && rows[0] == "ok" ? "ok" : string.Join("; ", rows.Take(20));
        }

        /// <inheritdoc />
        public async Task RebuildIndexesAsync(CancellationToken ct)
        {
            await NonQueryAsync(await ConnectionStringAsync(ct), "REINDEX", ct);
        }

        private async Task<string> ConnectionStringAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return db.Database.GetConnectionString() ?? throw new InvalidOperationException("The database has no connection string.");
        }

        private static async Task<T> ScalarAsync<T>(string connectionString, string sql, CancellationToken ct)
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            object value = await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException($"No value returned for {sql}.");
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private static async Task<List<string>> ColumnAsync(string connectionString, string sql, CancellationToken ct)
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
            var values = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        private static async Task NonQueryAsync(string connectionString, string sql, CancellationToken ct)
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            _ = await command.ExecuteNonQueryAsync(ct);
        }
    }
}
