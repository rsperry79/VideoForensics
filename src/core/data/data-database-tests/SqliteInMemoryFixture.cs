using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.Configurations;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Test fixture that maintains a single in-memory SQLite connection for the lifetime of a test.</summary>
    public class SqliteInMemoryFixture : IAsyncLifetime, IDisposable
    {
        private SqliteConnection? _connection;
        private IDbContextFactory<VideoForensicsDbContext>? _factory;
        private IDataProtectionProvider? _dataProtectionProvider;
        private ICredentialEncryptionProvider? _encryptionProvider;

        /// <summary>Gets the DbContext factory for this fixture.</summary>
        public IDbContextFactory<VideoForensicsDbContext> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized");

        /// <summary>Gets the credential encryption provider for this fixture.</summary>
        public ICredentialEncryptionProvider EncryptionProvider => _encryptionProvider ?? throw new InvalidOperationException("Fixture not initialized");

        /// <summary>Gets the data protection provider for this fixture.</summary>
        public IDataProtectionProvider DataProtectionProvider => _dataProtectionProvider ?? throw new InvalidOperationException("Fixture not initialized");

        /// <summary>Initializes the fixture by opening an in-memory SQLite connection and creating the schema.</summary>
        public async ValueTask InitializeAsync()
        {
            // Create an ephemeral in-memory SQLite connection that persists for the test lifetime
            _connection = new SqliteConnection("DataSource=:memory:");
            await _connection.OpenAsync();

            // Create DbContextOptions that use the open connection
            var options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite(_connection)
                .Options;

            // Create a simple factory that reuses the shared connection
            _factory = new TestDbContextFactory(options);

            // Set up data protection using an ephemeral provider (no filesystem needed)
            _dataProtectionProvider = new EphemeralDataProtectionProvider();
            _encryptionProvider = new CredentialEncryptionProvider(_dataProtectionProvider);

            // Create the schema using EnsureCreated
            await using var db = _factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        /// <summary>Cleans up the fixture by closing and disposing the connection.</summary>
        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        /// <summary>Simple test factory implementation that reuses the shared connection.</summary>
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
