using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.DependencyInjection;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for ServiceCollectionExtensions.AddVideoForensicsDatabase().</summary>
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddVideoForensicsDatabase_AllRepositoriesRegistered_ResolvesSuccessfully()
        {
            // Arrange
            var services = new ServiceCollection();

            // Set up logging (required by repositories)
            _ = services.AddLogging();

            // Set up in-memory SQLite DbContextFactory (prerequisite for repositories)
            SqliteConnection connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            try
            {
                DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                    .UseSqlite(connection)
                    .Options;

                services.AddSingleton<IDbContextFactory<VideoForensicsDbContext>>(
                    new TestDbContextFactory(options));

                // Act
                _ = services.AddVideoForensicsDatabase();
                ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

                // Assert - verify all three missing repositories can be resolved (via scope for scoped services)
                using IServiceScope scope = provider.CreateScope();
                IBannedIpRangeRepository bannedIpRepo = scope.ServiceProvider.GetRequiredService<IBannedIpRangeRepository>();
                ILockoutPolicySettingsRepository lockoutRepo = scope.ServiceProvider.GetRequiredService<ILockoutPolicySettingsRepository>();
                ITwoFactorRoleRequirementRepository twoFactorRepo = scope.ServiceProvider.GetRequiredService<ITwoFactorRoleRequirementRepository>();

                Assert.NotNull(bannedIpRepo);
                Assert.NotNull(lockoutRepo);
                Assert.NotNull(twoFactorRepo);

                provider.Dispose();
            }
            finally
            {
                connection.Dispose();
            }
        }

        /// <summary>Simple test factory implementation that reuses a shared connection.</summary>
        private class TestDbContextFactory : IDbContextFactory<VideoForensicsDbContext>
        {
            private readonly DbContextOptions<VideoForensicsDbContext> _options;

            public TestDbContextFactory(DbContextOptions<VideoForensicsDbContext> options)
            {
                _options = options;
            }

            public VideoForensicsDbContext CreateDbContext()
            {
                return new VideoForensicsDbContext(_options);
            }
        }
    }
}
