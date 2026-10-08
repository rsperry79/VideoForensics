using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Data.Common;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.Migrations;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Integration tests for DatabaseInitializer.</summary>
    public class DatabaseInitializerTests
    {
        private string GetTempDbPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);
            return Path.Combine(tempDir, "test.db");
        }

        [Fact]
        public async Task InitializeAsync_FreshDatabase_AppliesMigrationsAndCreatesFile()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);
                _ = services.AddLogging();

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                ILogger<DatabaseInitializerTests> logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert
                Assert.True(File.Exists(dbPath), "Database file should exist after initialization");

                // Verify migrations were applied - query a table
                await using VideoForensicsDbContext context = await factory.CreateDbContextAsync();
                List<User> users = await context.Users.ToListAsync();
                Assert.NotNull(users);

                await provider.DisposeAsync();
                // Give SQLite time to release the file lock
                await Task.Delay(100);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // SQLite file may still be locked, try again after a longer delay
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_ExistingDatabaseWithoutPendingMigrations_DoesNotThrow()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);
                _ = services.AddLogging();

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                ILogger<DatabaseInitializerTests> logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Initialize the database for the first time
                await DatabaseInitializer.InitializeAsync(factory, logger);
                Assert.True(File.Exists(dbPath));

                // Act - Call InitializeAsync a second time (should be idempotent, no pending migrations)
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert - No exception thrown
                Assert.True(File.Exists(dbPath));

                await provider.DisposeAsync();
                // Give SQLite time to release the file lock
                await Task.Delay(100);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // SQLite file may still be locked, try again after a longer delay
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_AfterMigration_EnablesWalMode()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);
                _ = services.AddLogging();

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                ILogger<DatabaseInitializerTests> logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert - Check WAL mode via SQL pragma
                await using VideoForensicsDbContext context = await factory.CreateDbContextAsync();
                DbConnection connection = context.Database.GetDbConnection();

                await connection.OpenAsync();
                using DbCommand command = connection.CreateCommand();
                command.CommandText = "PRAGMA journal_mode;";
                string? result = await command.ExecuteScalarAsync() as string;

                Assert.Equal("wal", result);

                await provider.DisposeAsync();
                // Give SQLite time to release the file lock
                await Task.Delay(100);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // SQLite file may still be locked, try again after a longer delay
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_ValidDatabase_LogsCompletionSuccessfully()
        {
            // PRAGMA integrity_check was deliberately removed from the startup path - it scans
            // the entire database and was a major contributor to the 30-60s startup timeouts this
            // app used to see (see DatabaseInitializer.InitializeAsync's "Skip integrity check for
            // performance" comment). This test previously asserted the now-removed "integrity check
            // passed" log line; it now asserts the log line startup actually produces.

            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var logMessages = new List<(LogLevel, string)>();

                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);
                _ = services.AddLogging(builder =>
                {
                    _ = builder.AddProvider(new TestLoggerProvider(logMessages));
                });

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                ILogger<DatabaseInitializerTests> logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert
                Assert.True(
                    logMessages.Any(m => m.Item1 == LogLevel.Information && m.Item2.Contains("Database initialization completed successfully")),
                    "Expected an Information-level log entry confirming database initialization completed");
                Assert.DoesNotContain(logMessages, m => m.Item1 == LogLevel.Error);

                await provider.DisposeAsync();
                // Give SQLite time to release the file lock
                await Task.Delay(100);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // SQLite file may still be locked, try again after a longer delay
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        private static void DeleteTempDir(string tempDir)
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(tempDir))
            {
                return;
            }

            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                try
                { Directory.Delete(tempDir, recursive: true); }
                catch { }
            }
        }

        private static ServiceProvider BuildProvider(string dbPath)
        {
            var services = new ServiceCollection();
            _ = services.AddVideoForensicsSqlite(dbPath);
            _ = services.AddLogging();
            return services.BuildServiceProvider();
        }

        private static void CreateHistoryOnlyDatabase(string dbPath, params string[] migrationIds)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using SqliteCommand create = connection.CreateCommand();
                create.CommandText = "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
                _ = create.ExecuteNonQuery();
                foreach (string id in migrationIds)
                {
                    using SqliteCommand insert = connection.CreateCommand();
                    insert.CommandText = "INSERT INTO \"__EFMigrationsHistory\" VALUES ($id, '10.0.0');";
                    _ = insert.Parameters.AddWithValue("$id", id);
                    _ = insert.ExecuteNonQuery();
                }
            }

            SqliteConnection.ClearAllPools();
        }

        [Fact]
        public async Task GetMigrations_AfterConsolidation_ContainsExactlyOneInitialCreate()
        {
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;
            try
            {
                await using ServiceProvider provider = BuildProvider(dbPath);
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                await using VideoForensicsDbContext context = await factory.CreateDbContextAsync();

                string[] migrations = context.Database.GetMigrations().ToArray();

                _ = Assert.Single(migrations);
                Assert.EndsWith("_InitialCreate", migrations[0]);
            }
            finally
            {
                DeleteTempDir(tempDir);
            }
        }

        [Fact]
        public async Task InitializeAsync_LegacyAlphaMigrationHistory_ThrowsActionableErrorAndLogsIt()
        {
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;
            try
            {
                CreateHistoryOnlyDatabase(dbPath, "20260928173811_InitialCreate", "20261008000659_AddOperatorPreferencesUiModeLocked");
                var logMessages = new List<(LogLevel, string)>();
                await using ServiceProvider provider = BuildProvider(dbPath);
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => DatabaseInitializer.InitializeAsync(factory, new TestLogger(logMessages)));

                Assert.Contains(dbPath, ex.Message);
                Assert.Contains("delete", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("older alpha", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(logMessages, m => m.Item1 == LogLevel.Error && m.Item2.Contains(dbPath));
                Assert.True(File.Exists(dbPath), "The stale database must not be deleted automatically");
            }
            finally
            {
                DeleteTempDir(tempDir);
            }
        }

        [Fact]
        public async Task InitializeAsync_HistoryContainsOnlyCurrentMigration_DoesNotThrow()
        {
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;
            try
            {
                await using ServiceProvider provider = BuildProvider(dbPath);
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                string current;
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    current = context.Database.GetMigrations().Single();
                }

                CreateHistoryOnlyDatabase(dbPath, current);

                await DatabaseInitializer.InitializeAsync(factory, new TestLogger(new List<(LogLevel, string)>()));
            }
            finally
            {
                DeleteTempDir(tempDir);
            }
        }

        [Fact]
        public async Task InitializeAsync_EmptyDatabaseFileWithoutHistoryTable_Works()
        {
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;
            try
            {
                File.WriteAllBytes(dbPath, Array.Empty<byte>());
                await using ServiceProvider provider = BuildProvider(dbPath);
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                await DatabaseInitializer.InitializeAsync(factory, new TestLogger(new List<(LogLevel, string)>()));

                await using VideoForensicsDbContext context = await factory.CreateDbContextAsync();
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            }
            finally
            {
                DeleteTempDir(tempDir);
            }
        }

        [Fact]
        public async Task InitialCreate_Schema_MatchesCurrentModel()
        {
            string migratedPath = GetTempDbPath();
            string createdPath = GetTempDbPath();
            string migratedDir = Path.GetDirectoryName(migratedPath)!;
            string createdDir = Path.GetDirectoryName(createdPath)!;
            try
            {
                await using (ServiceProvider migrated = BuildProvider(migratedPath))
                {
                    await DatabaseInitializer.InitializeAsync(
                        migrated.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>(),
                        new TestLogger(new List<(LogLevel, string)>()));
                }

                await using (ServiceProvider created = BuildProvider(createdPath))
                {
                    await using VideoForensicsDbContext context = await created.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>().CreateDbContextAsync();
                    _ = await context.Database.EnsureCreatedAsync();
                }

                SqliteConnection.ClearAllPools();
                List<string> fromMigration = DescribeSchema(migratedPath);
                List<string> fromModel = DescribeSchema(createdPath);

                Assert.NotEmpty(fromModel);
                Assert.Equal(fromModel, fromMigration);
            }
            finally
            {
                DeleteTempDir(migratedDir);
                DeleteTempDir(createdDir);
            }
        }

        /// <summary>Flattens tables, columns, foreign keys and named indexes into sorted comparable lines (ignores the EF history/lock bookkeeping tables).</summary>
        private static List<string> DescribeSchema(string dbPath)
        {
            var lines = new List<string>();
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();

            var tables = new List<string>();
            using (SqliteCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%' ORDER BY name;";
                using SqliteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    tables.Add(r.GetString(0));
                }
            }

            foreach (string table in tables)
            {
                lines.Add($"TABLE {table}");
                Collect(connection, $"PRAGMA table_info(\"{table}\");", r => $"COL {table}.{r["name"]} {r["type"]} notnull={r["notnull"]} dflt={r["dflt_value"]} pk={r["pk"]}", lines);
                Collect(connection, $"PRAGMA foreign_key_list(\"{table}\");", r => $"FK {table}.{r["from"]} -> {r["table"]}.{r["to"]} del={r["on_delete"]}", lines);

                var indexNames = new List<(string Name, string Unique)>();
                using (SqliteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA index_list(\"{table}\");";
                    using SqliteDataReader r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string name = r.GetString(r.GetOrdinal("name"));
                        if (!name.StartsWith("sqlite_autoindex", StringComparison.Ordinal))
                        {
                            indexNames.Add((name, r["unique"].ToString()!));
                        }
                    }
                }

                foreach ((string name, string unique) in indexNames)
                {
                    lines.Add($"IDX {table}.{name} unique={unique}");
                    Collect(connection, $"PRAGMA index_info(\"{name}\");", r => $"IDXCOL {name} {r["seqno"]} {r["name"]}", lines);
                }
            }

            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        private static void Collect(SqliteConnection connection, string sql, Func<SqliteDataReader, string> format, List<string> lines)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            using SqliteDataReader r = cmd.ExecuteReader();
            while (r.Read())
            {
                lines.Add(format(r));
            }
        }
        /// <summary>Simple test logger provider for capturing log messages.</summary>
        private class TestLoggerProvider : ILoggerProvider
        {
            private readonly List<(LogLevel, string)> _messages;

            public TestLoggerProvider(List<(LogLevel, string)> messages)
            {
                _messages = messages;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new TestLogger(_messages);
            }

            public void Dispose()
            {
            }
        }

        /// <summary>Simple test logger for capturing log messages.</summary>
        private class TestLogger : ILogger
        {
            private readonly List<(LogLevel, string)> _messages;

            public TestLogger(List<(LogLevel, string)> messages)
            {
                _messages = messages;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                string message = formatter(state, exception);
                _messages.Add((logLevel, message));
            }
        }
    }
}
