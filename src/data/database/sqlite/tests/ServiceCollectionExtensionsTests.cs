using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Tests for ServiceCollectionExtensions.AddVideoForensicsSqlite().</summary>
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddVideoForensicsSqlite_ExplicitPath_ResolvesFactory()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string dbPath = Path.Combine(tempDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                // Act
                _ = services.AddVideoForensicsSqlite(dbPath);
                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext>? factory = provider.GetService<IDbContextFactory<VideoForensicsDbContext>>();

                // Assert
                Assert.NotNull(factory);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        [Fact]
        public void AddVideoForensicsSqlite_CustomPath_UsesProvidedPath()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string dbPath = Path.Combine(tempDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                // Act
                _ = services.AddVideoForensicsSqlite(dbPath);
                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext>? factory = provider.GetService<IDbContextFactory<VideoForensicsDbContext>>();

                Assert.NotNull(factory);

                // Create a context and verify connection string references the expected path
                using VideoForensicsDbContext context = factory!.CreateDbContext();
                string connectionString = context.Database.GetDbConnection().ConnectionString;

                // Assert
                Assert.Contains(dbPath, connectionString);

                provider.Dispose();
                // Give SQLite time to release the file lock
                System.Threading.Thread.Sleep(100);
            }
            finally
            {
                // Cleanup
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
        public void AddVideoForensicsSqlite_DefaultOptions_UsesSplitQuerySplittingBehavior()
        {
            // Arrange: multi-collection Includes warn under the default SingleQuery behavior, so the factory must opt in to split queries.
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string dbPath = Path.Combine(tempDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                _ = services.AddVideoForensicsSqlite(dbPath);
                using ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                // Act
                using VideoForensicsDbContext context = factory.CreateDbContext();
                // FindExtension matches the exact type and the SQLite extension is internal, so scan the public base type instead.
                RelationalOptionsExtension? relational = context.GetService<IDbContextOptions>()
                    .Extensions.OfType<RelationalOptionsExtension>().SingleOrDefault();

                // Assert
                Assert.NotNull(relational);
                Assert.Equal(QuerySplittingBehavior.SplitQuery, relational!.QuerySplittingBehavior);
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
                        // SQLite file may still be locked; cleanup is best-effort.
                    }
                }
            }
        }

        [Fact]
        public void AddVideoForensicsSqlite_ParentDirectoryMissing_CreatesDirectory()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string nestedDir = Path.Combine(tempDir, "nested", "path");
            string dbPath = Path.Combine(nestedDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                // Verify directory doesn't exist yet
                Assert.False(Directory.Exists(nestedDir));

                // Act
                _ = services.AddVideoForensicsSqlite(dbPath);

                // Assert
                Assert.True(Directory.Exists(nestedDir), "Expected directory to be created");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }
}
