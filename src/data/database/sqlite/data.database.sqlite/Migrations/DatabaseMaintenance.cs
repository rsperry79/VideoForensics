using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>Handles database maintenance tasks with intelligent threshold-based execution.</summary>
    public static class DatabaseMaintenance
    {
        private const string LastMaintenanceKey = "database.last_maintenance_utc";
        private const int MaintenanceIntervalHours = 24;
        private const int WalSizeThresholdBytes = 100 * 1024; // 100KB
        private const int DbSizeThresholdBytes = 5 * 1024 * 1024; // 5MB

        /// <summary>
        /// Check if database maintenance should run based on multiple thresholds:
        /// - WAL file size (>100KB)
        /// - Database file size (>5MB)
        /// - Time since last maintenance (>24 hours)
        /// - Query performance (test query execution time)
        /// </summary>
        public static async Task<MaintenanceDiagnosis> DiagnoseAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var diagnosis = new MaintenanceDiagnosis();

            try
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken);
                var connection = db.Database.GetDbConnection() as SqliteConnection;
                if (connection?.DataSource == null)
                {
                    logger.LogInformation("Database maintenance: not a file-based database, skipping diagnosis.");
                    return diagnosis;
                }

                // 1. Check WAL file size
                var dbPath = connection.DataSource;
                var walPath = $"{dbPath}-wal";
                if (File.Exists(walPath))
                {
                    var walSize = new FileInfo(walPath).Length;
                    diagnosis.WalSizeBytes = walSize;
                    diagnosis.NeedsWalCheckpoint = walSize > WalSizeThresholdBytes;
                    if (diagnosis.NeedsWalCheckpoint)
                        logger.LogInformation("Database maintenance: WAL file is {WalSizeMB:F1}MB (threshold: 100KB)", walSize / 1024.0 / 1024.0);
                }

                // 2. Check database file size
                if (File.Exists(dbPath))
                {
                    var dbSize = new FileInfo(dbPath).Length;
                    diagnosis.DatabaseSizeBytes = dbSize;
                    diagnosis.NeedsReindex = dbSize > DbSizeThresholdBytes;
                    if (diagnosis.NeedsReindex)
                        logger.LogInformation("Database maintenance: Database is {DbSizeMB:F1}MB (threshold: 5MB)", dbSize / 1024.0 / 1024.0);
                }

                // 3. Check time since last maintenance
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken);

                var lastMaintenance = await GetLastMaintenanceTimeAsync(db, cancellationToken);
                if (lastMaintenance.HasValue)
                {
                    var hoursSinceLastMaintenance = (DateTime.UtcNow - lastMaintenance.Value).TotalHours;
                    diagnosis.HoursSinceLastMaintenance = hoursSinceLastMaintenance;
                    diagnosis.NeedsTimedMaintenance = hoursSinceLastMaintenance > MaintenanceIntervalHours;
                    if (diagnosis.NeedsTimedMaintenance)
                        logger.LogInformation("Database maintenance: Last maintenance was {Hours:F1} hours ago (threshold: 24h)", hoursSinceLastMaintenance);
                }
                else
                {
                    diagnosis.NeedsTimedMaintenance = true;
                    logger.LogInformation("Database maintenance: No maintenance history found, will perform maintenance.");
                }

                // 4. Profile query performance (quick test)
                diagnosis.QueryMs = await MeasureQueryPerformanceAsync(connection, logger, cancellationToken);
                diagnosis.NeedsOptimization = diagnosis.QueryMs > 50; // If test query takes >50ms, optimize

                diagnosis.ShouldMaintain = diagnosis.NeedsWalCheckpoint ||
                                          diagnosis.NeedsReindex ||
                                          diagnosis.NeedsTimedMaintenance ||
                                          diagnosis.NeedsOptimization;

                return diagnosis;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database maintenance diagnosis failed, will proceed with full maintenance.");
                diagnosis.ShouldMaintain = true; // Conservative: run maintenance on diagnosis failure
                return diagnosis;
            }
        }

        /// <summary>
        /// Run non-blocking database maintenance: checkpoint WAL, optimize indexes, and analyze query performance.
        /// Respects threshold-based execution from diagnosis.
        /// </summary>
        public static async Task OptimizeAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger logger,
            MaintenanceDiagnosis? diagnosis = null,
            CancellationToken cancellationToken = default)
        {
            if (diagnosis == null)
            {
                diagnosis = await DiagnoseAsync(factory, logger, cancellationToken);
            }

            if (!diagnosis.ShouldMaintain)
            {
                logger.LogInformation("Database maintenance: Database health is good, skipping maintenance. (WAL: {WalMB:F1}MB, DB: {DbMB:F1}MB, Query: {QueryMs:F0}ms, Last: {HoursSince:F1}h ago)",
                    diagnosis.WalSizeBytes / 1024.0 / 1024.0,
                    diagnosis.DatabaseSizeBytes / 1024.0 / 1024.0,
                    diagnosis.QueryMs,
                    diagnosis.HoursSinceLastMaintenance ?? 0);
                return;
            }

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
                if (diagnosis.NeedsWalCheckpoint)
                {
                    logger.LogInformation("Database maintenance: checkpointing WAL ({WalMB:F1}MB)...", diagnosis.WalSizeBytes / 1024.0 / 1024.0);
                    command.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                // 2. Reindex to rebuild corrupted or fragmented indexes
                if (diagnosis.NeedsReindex)
                {
                    logger.LogInformation("Database maintenance: reindexing ({DbMB:F1}MB database)...", diagnosis.DatabaseSizeBytes / 1024.0 / 1024.0);
                    command.CommandText = "REINDEX;";
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                // 3. Optimize query plans
                if (diagnosis.NeedsOptimization)
                {
                    logger.LogInformation("Database maintenance: optimizing queries ({QueryMs:F0}ms baseline)...", diagnosis.QueryMs);
                    command.CommandText = "PRAGMA optimize;";
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                // 4. Analyze table statistics for query planner
                if (diagnosis.NeedsTimedMaintenance || diagnosis.NeedsOptimization)
                {
                    logger.LogInformation("Database maintenance: analyzing statistics...");
                    command.CommandText = "ANALYZE;";
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                // Record maintenance completion
                await RecordMaintenanceAsync(db, cancellationToken);
                logger.LogInformation("Database maintenance completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database maintenance failed (non-fatal) - performance may be degraded.");
            }
        }

        private static async Task<double> MeasureQueryPerformanceAsync(
            System.Data.Common.DbConnection connection,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master;"; // Fast diagnostic query
                await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            catch
            {
                return 0; // Can't measure, assume it's fine
            }
        }

        private static async Task<DateTime?> GetLastMaintenanceTimeAsync(
            VideoForensicsDbContext db,
            CancellationToken cancellationToken)
        {
            try
            {
                var setting = await db.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == LastMaintenanceKey, cancellationToken);

                if (setting != null && DateTime.TryParse(setting.Value, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var lastMaintenance))
                {
                    return lastMaintenance;
                }
            }
            catch
            {
                // If we can't read settings, assume we need maintenance
            }

            return null;
        }

        private static async Task RecordMaintenanceAsync(
            VideoForensicsDbContext db,
            CancellationToken cancellationToken)
        {
            try
            {
                var connection = db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using var command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO AppSettings (Key, Value) VALUES ('{LastMaintenanceKey}', '{DateTime.UtcNow:O}');";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Non-fatal: if we can't record it, just continue
            }
        }
    }

    /// <summary>Diagnostic results for database maintenance decision-making.</summary>
    public class MaintenanceDiagnosis
    {
        public long WalSizeBytes { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public double? HoursSinceLastMaintenance { get; set; }
        public double QueryMs { get; set; }

        public bool NeedsWalCheckpoint { get; set; }
        public bool NeedsReindex { get; set; }
        public bool NeedsTimedMaintenance { get; set; }
        public bool NeedsOptimization { get; set; }

        public bool ShouldMaintain { get; set; }
    }
}
