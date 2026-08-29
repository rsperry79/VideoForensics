using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>Handles database maintenance tasks for optimal SQLite performance.</summary>
    public static class DatabaseMaintenance
    {
        /// <summary>
        /// Run non-blocking database maintenance: checkpoint WAL, optimize indexes, and analyze query performance.
        /// This should be called asynchronously after initialization to improve query speeds.
        /// </summary>
        public static async Task OptimizeAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken);
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = connection.CreateCommand();

                // 1. Checkpoint WAL file (flush pending writes to main DB)
                logger.LogInformation("Database maintenance: checkpointing WAL...");
                command.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                await command.ExecuteScalarAsync(cancellationToken);

                // 2. Reindex to rebuild corrupted or fragmented indexes
                logger.LogInformation("Database maintenance: reindexing...");
                command.CommandText = "REINDEX;";
                await command.ExecuteScalarAsync(cancellationToken);

                // 3. Optimize query plans
                logger.LogInformation("Database maintenance: optimizing queries...");
                command.CommandText = "PRAGMA optimize;";
                await command.ExecuteScalarAsync(cancellationToken);

                // 4. Analyze table statistics for query planner
                logger.LogInformation("Database maintenance: analyzing statistics...");
                command.CommandText = "ANALYZE;";
                await command.ExecuteScalarAsync(cancellationToken);

                logger.LogInformation("Database maintenance completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database maintenance failed (non-fatal) - performance may be degraded.");
            }
        }
    }
}
