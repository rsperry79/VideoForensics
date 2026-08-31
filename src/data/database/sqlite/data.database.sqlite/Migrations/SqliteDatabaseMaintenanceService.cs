using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>SQLite implementation of <see cref="IDatabaseMaintenanceService"/>.</summary>
    internal class SqliteDatabaseMaintenanceService : IDatabaseMaintenanceService
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SqliteDatabaseMaintenanceService> _logger;

        public SqliteDatabaseMaintenanceService(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<SqliteDatabaseMaintenanceService> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task ReleaseAllConnectionsAsync(CancellationToken ct = default)
        {
            try
            {
                // TRUNCATE checkpoint flushes every pending WAL frame into the main database file
                // and truncates the WAL back to zero bytes - a plain PASSIVE checkpoint (used by
                // routine maintenance) can leave data in the WAL if a reader is active, which would
                // still block deleting the file cleanly afterward.
                await using (var db = await _factory.CreateDbContextAsync(ct))
                {
                    var connection = db.Database.GetDbConnection() as SqliteConnection;
                    if (connection != null)
                    {
                        if (connection.State != System.Data.ConnectionState.Open)
                        {
                            await connection.OpenAsync(ct);
                        }

                        using var command = connection.CreateCommand();
                        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        await command.ExecuteScalarAsync(ct);
                    }
                }

                // The connection above is returned to the ADO.NET pool on dispose, not actually
                // closed at the OS level - ClearAllPools() is what actually releases every pooled
                // connection's underlying file handle for every SqliteConnection in this process.
                SqliteConnection.ClearAllPools();

                _logger.LogInformation("Released all pooled database connections.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanly release database connections before file operation.");
                // Still clear pools even if the checkpoint failed - a partial release is better
                // than none for the caller's subsequent file operation.
                SqliteConnection.ClearAllPools();
            }
        }
    }
}
