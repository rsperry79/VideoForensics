using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.Migrations;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Regression tests for SQLITE_LOCKED ("database table is locked") between concurrent connections.</summary>
    public class SqliteLockingTests
    {
        [Fact]
        public async Task Write_WhileAnotherConnectionReadsTable_Succeeds()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string dbPath = Path.Combine(tempDir, "test.db");

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);
                _ = services.AddLogging();

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                ILogger<SqliteLockingTests> logger = provider.GetRequiredService<ILogger<SqliteLockingTests>>();

                await DatabaseInitializer.InitializeAsync(factory, logger);

                // A reader holds an open read transaction on Users, as a long query would. A deferred transaction takes
                // only a read snapshot, so it does not block a writer in WAL mode. EF's BeginTransactionAsync is avoided
                // here because it takes the write lock up front.
                await using var reader = new SqliteConnection(SqliteConnectionStrings.ForFile(dbPath));
                await reader.OpenAsync();
                await using SqliteTransaction readTx = reader.BeginTransaction(deferred: true);
                await using (SqliteCommand read = reader.CreateCommand())
                {
                    read.Transaction = readTx;
                    read.CommandText = "SELECT COUNT(*) FROM Users;";
                    _ = await read.ExecuteScalarAsync();
                }

                // Act - write to the same table from another context while the read transaction is still open
                await using VideoForensicsDbContext writer = await factory.CreateDbContextAsync();
                _ = writer.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    ProviderUserKey = "lock-test",
                    DisplayName = "Lock Test",
                    Email = "lock@example.com",
                    CreatedUtc = DateTime.UtcNow
                });

                // Assert - no SQLITE_LOCKED
                _ = await writer.SaveChangesAsync();

                await readTx.RollbackAsync();

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
    }
}
