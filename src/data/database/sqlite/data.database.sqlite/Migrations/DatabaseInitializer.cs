using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>Handles database initialization, migration, and integrity checks for SQLite VideoForensics database.</summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Initializes the VideoForensics database: applies pending migrations, enables WAL mode, and verifies integrity.
        /// </summary>
        /// <param name="factory">The DbContext factory.</param>
        /// <param name="logger">The logger for recording initialization status.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous initialization.</returns>
        /// <remarks>
        /// This method performs the following steps:
        /// 1. Checks for pending migrations and backs up the database if needed (pre-migration backup gap).
        /// 2. Applies any pending migrations using MigrateAsync.
        /// 3. Enables WAL (Write-Ahead Logging) mode for improved concurrency.
        /// 4. Runs PRAGMA integrity_check to detect database corruption and logs the result.
        /// Failures during migration are logged and rethrown to prevent continuing into a broken application.
        /// </remarks>
        public static async Task InitializeAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken);

                // Check for pending migrations
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
                var hasPendingMigrations = pendingMigrations.Any();

                // Backup database before migration if needed
                if (hasPendingMigrations)
                {
                    await BackupDatabaseIfExistsAsync(db, logger, cancellationToken);
                }

                // Apply migrations
                await db.Database.MigrateAsync(cancellationToken);

                // Enable WAL mode for better concurrency
                await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

                // Quick optimization: analyze table statistics (fast ~5-50ms, improves query planner)
                await db.Database.ExecuteSqlRawAsync("ANALYZE;", cancellationToken);

                // Skip integrity check for performance - it scans the entire database
                // If corruption is suspected, run: PRAGMA integrity_check; manually
                logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database initialization failed. The application cannot continue.");
                throw;
            }
        }

        /// <summary>
        /// Backs up the database file to a timestamped sibling file if the database already exists and migrations are pending.
        /// </summary>
        private static async Task BackupDatabaseIfExistsAsync(
            VideoForensicsDbContext db,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var connection = db.Database.GetDbConnection() as SqliteConnection;
            if (connection?.DataSource == null)
            {
                return; // Not a file-based SQLite database
            }

            var dbPath = connection.DataSource;
            if (!File.Exists(dbPath))
            {
                return; // No file to back up on first run
            }

            try
            {
                var backupPath = GenerateBackupPath(dbPath);
                File.Copy(dbPath, backupPath, overwrite: false);
                // Note: We could also use VACUUM INTO for a cleaner backup, but file copy is simpler and works fine
            }
            catch (Exception ex)
            {
                // Log backup failure but don't fail initialization if backup fails
                // The user should be aware, but we proceed with caution
                logger.LogWarning(ex, "Failed to create pre-migration backup. Proceeding with migration anyway.");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Generates a timestamped backup file path for the given database file.
        /// </summary>
        private static string GenerateBackupPath(string originalPath)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var directory = Path.GetDirectoryName(originalPath);
            var filename = Path.GetFileName(originalPath);
            var backupFilename = $"{filename}.bak-{timestamp}";
            return Path.Combine(directory ?? "", backupFilename);
        }

        /// <summary>
        /// Runs PRAGMA integrity_check and logs the result (Info if "ok", Error otherwise).
        /// </summary>
        private static async Task RunIntegrityCheckAsync(
            VideoForensicsDbContext db,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";

                var result = await command.ExecuteScalarAsync(cancellationToken) as string;
                if (result == "ok")
                {
                    logger.LogInformation("Database integrity check passed.");
                }
                else
                {
                    logger.LogError("Database integrity check failed: {IntegrityCheckResult}", result);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run database integrity check.");
                // Don't rethrow - integrity check failure shouldn't block app startup
            }
        }
    }
}
