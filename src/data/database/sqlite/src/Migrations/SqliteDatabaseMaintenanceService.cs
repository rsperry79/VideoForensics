using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>SQLite implementation of <see cref="IDatabaseMaintenanceService"/>.</summary>
    internal class SqliteDatabaseMaintenanceService : IDatabaseMaintenanceService
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SqliteDatabaseMaintenanceService> _logger;
        private readonly ITransactionlessWriteGate _gate;

        public SqliteDatabaseMaintenanceService(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ITransactionlessWriteGate gate,
            ILogger<SqliteDatabaseMaintenanceService> logger)
        {
            _factory = factory;
            _gate = gate;
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
                // Checkpoints cannot run inside a transaction, so they go through the write gate on a transaction-less context.
                _ = await _gate.ExecuteWithoutTransactionAsync(async (db, token) =>
                {
                    if (db.Database.GetDbConnection() is SqliteConnection connection)
                    {
                        if (connection.State != System.Data.ConnectionState.Open)
                        {
                            await connection.OpenAsync(token);
                        }

                        using SqliteCommand command = connection.CreateCommand();
                        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        _ = await command.ExecuteScalarAsync(token);
                    }

                    return 0;
                }, ct);

                // The connection above is returned to the ADO.NET pool on dispose, not actually
                // closed at the OS level - ClearAllPools() is what actually releases every pooled
                // connection's underlying file handle for every SqliteConnection in this process.
                SqliteConnection.ClearAllPools();

                _logger.LogDebug("Released all pooled database connections.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to cleanly release database connections before file operation.: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                // Still clear pools even if the checkpoint failed - a partial release is better
                // than none for the caller's subsequent file operation.
                SqliteConnection.ClearAllPools();
            }
        }
    }
}
