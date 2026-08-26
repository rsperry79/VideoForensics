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
    /// <summary>Tests for concurrent DbContext creation and usage.</summary>
    public class ConcurrencyTests
    {
        [Fact]
        public async Task DbContextFactory_ConcurrentContextCreation_EachContextIsIndependent()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var dbPath = Path.Combine(tempDir, "test.db");

            try
            {
                var services = new ServiceCollection();
                services.AddVideoForensicsSqlite(dbPath);
                services.AddLogging();

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                var logger = provider.GetRequiredService<ILogger<ConcurrencyTests>>();

                // Initialize the database
                await DatabaseInitializer.InitializeAsync(factory, logger);

                // Act - Create two contexts concurrently
                var context1Task = factory.CreateDbContextAsync();
                var context2Task = factory.CreateDbContextAsync();

                await using var context1 = await context1Task;
                await using var context2 = await context2Task;

                // Assert - Contexts are distinct instances
                Assert.NotSame(context1, context2);

                // Insert a user via context1
                var testUser = new User
                {
                    Id = Guid.NewGuid(),
                    ProviderUserKey = "test-key",
                    DisplayName = "Test User",
                    Email = "test@example.com",
                    CreatedUtc = DateTime.UtcNow
                };

                context1.Users.Add(testUser);
                await context1.SaveChangesAsync();

                // Read via context2 - should see the inserted data
                var users = await context2.Users.ToListAsync();
                Assert.NotEmpty(users);
                Assert.Contains(users, u => u.ProviderUserKey == "test-key");

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
    }
}
