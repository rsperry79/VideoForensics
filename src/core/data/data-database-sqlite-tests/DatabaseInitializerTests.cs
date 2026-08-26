using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            return Path.Combine(tempDir, "test.db");
        }

        [Fact]
        public async Task InitializeAsync_FreshDatabase_AppliesMigrationsAndCreatesFile()
        {
            // Arrange
            var dbPath = GetTempDbPath();
            var tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                services.AddVideoForensicsSqlite(dbPath);
                services.AddLogging();

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                var logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert
                Assert.True(File.Exists(dbPath), "Database file should exist after initialization");

                // Verify migrations were applied - query a table
                await using var context = await factory.CreateDbContextAsync();
                var users = await context.Users.ToListAsync();
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
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_ExistingDatabaseWithoutPendingMigrations_DoesNotThrow()
        {
            // Arrange
            var dbPath = GetTempDbPath();
            var tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                services.AddVideoForensicsSqlite(dbPath);
                services.AddLogging();

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                var logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

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
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_AfterMigration_EnablesWalMode()
        {
            // Arrange
            var dbPath = GetTempDbPath();
            var tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                services.AddVideoForensicsSqlite(dbPath);
                services.AddLogging();

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                var logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert - Check WAL mode via SQL pragma
                await using var context = await factory.CreateDbContextAsync();
                var connection = context.Database.GetDbConnection();

                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA journal_mode;";
                var result = await command.ExecuteScalarAsync() as string;

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
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task InitializeAsync_ValidDatabase_LogsIntegrityCheckPassed()
        {
            // Arrange
            var dbPath = GetTempDbPath();
            var tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var logMessages = new List<(LogLevel, string)>();

                var services = new ServiceCollection();
                services.AddVideoForensicsSqlite(dbPath);
                services.AddLogging(builder =>
                {
                    builder.AddProvider(new TestLoggerProvider(logMessages));
                });

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                var logger = provider.GetRequiredService<ILogger<DatabaseInitializerTests>>();

                // Act
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Assert
                Assert.True(
                    logMessages.Any(m => m.Item1 == LogLevel.Information && m.Item2.Contains("integrity check passed")),
                    "Expected an Information-level log entry mentioning integrity check passed");

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
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                    }
                }
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

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _messages.Add((logLevel, message));
            }
        }
    }
}
