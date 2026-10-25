using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>In-memory logs database for one test: the operational tables only, separate from the main fixture.</summary>
    public class SqliteLogsInMemoryFixture : IAsyncLifetime, IDisposable
    {
        private Microsoft.Data.Sqlite.SqliteConnection? _connection;
        private IDbContextFactory<VideoForensicsLogsDbContext>? _factory;

        public IDbContextFactory<VideoForensicsLogsDbContext> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized");

        public async ValueTask InitializeAsync()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            await _connection.OpenAsync();

            DbContextOptions<VideoForensicsLogsDbContext> options = new DbContextOptionsBuilder<VideoForensicsLogsDbContext>()
                .UseSqlite(_connection)
                .Options;
            _factory = new LogsTestFactory(options);

            await using VideoForensicsLogsDbContext db = await _factory.CreateDbContextAsync();
            _ = await db.Database.EnsureCreatedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }

        public void Dispose()
        {
        }

        private sealed class LogsTestFactory(DbContextOptions<VideoForensicsLogsDbContext> options) : IDbContextFactory<VideoForensicsLogsDbContext>
        {
            public VideoForensicsLogsDbContext CreateDbContext() => new(options);
        }
    }
}
