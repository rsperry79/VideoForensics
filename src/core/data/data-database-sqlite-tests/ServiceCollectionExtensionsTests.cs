using Microsoft.EntityFrameworkCore;
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
        public void AddVideoForensicsSqlite_DefaultPath_ResolvesFactory()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddVideoForensicsSqlite();
            var provider = services.BuildServiceProvider();
            var factory = provider.GetService<IDbContextFactory<VideoForensicsDbContext>>();

            // Assert
            Assert.NotNull(factory);
        }

        [Fact]
        public void AddVideoForensicsSqlite_CustomPath_UsesProvidedPath()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var dbPath = Path.Combine(tempDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                // Act
                services.AddVideoForensicsSqlite(dbPath);
                var provider = services.BuildServiceProvider();
                var factory = provider.GetService<IDbContextFactory<VideoForensicsDbContext>>();

                Assert.NotNull(factory);

                // Create a context and verify connection string references the expected path
                using var context = factory!.CreateDbContext();
                var connectionString = context.Database.GetDbConnection().ConnectionString;

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
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                    }
                }
            }
        }

        [Fact]
        public void AddVideoForensicsSqlite_ParentDirectoryMissing_CreatesDirectory()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var nestedDir = Path.Combine(tempDir, "nested", "path");
            var dbPath = Path.Combine(nestedDir, "test.db");
            var services = new ServiceCollection();

            try
            {
                // Verify directory doesn't exist yet
                Assert.False(Directory.Exists(nestedDir));

                // Act
                services.AddVideoForensicsSqlite(dbPath);

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
