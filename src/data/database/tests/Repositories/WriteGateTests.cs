using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Gate behaviour on a file-backed, pooled SQLite database, as production runs. The shared in-memory fixture uses one
    /// connection, which serialises everything and so hides the lock contention these tests are about.
    /// </summary>
    public class WriteGateTests : IAsyncLifetime
    {
        private string _databasePath = null!;
        private IDbContextFactory<VideoForensicsDbContext> _factory = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"writegate-{Guid.NewGuid():N}.db");
            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite($"Data Source={_databasePath};Pooling=true;Default Timeout=5")
                .Options;
            _factory = new PooledDbContextFactory(options);

            await using VideoForensicsDbContext db = _factory.CreateDbContext();
            _ = await db.Database.EnsureCreatedAsync();

            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _unitOfWork = new UnitOfWork(_factory, new ServiceCollection().AddLogging().BuildServiceProvider(), loggerFactory.CreateLogger<UnitOfWork>());
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // A pooled connection can still hold the file on Windows; a temp file is harmless to leave behind.
            }

            return ValueTask.CompletedTask;
        }

        [Fact]
        public async Task ConcurrentWrites_NeverRunAtTheSameTime()
        {
            // Characterises today's behaviour: the chain gate already serialises unit-of-work writes. Kept so U2 cannot regress it.
            int inside = 0;
            int maxInside = 0;

            Task[] writes = Enumerable.Range(0, 10)
                .Select(_ => _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    int now = Interlocked.Increment(ref inside);
                    InterlockedMax(ref maxInside, now);
                    await Task.Delay(20);
                    _ = Interlocked.Decrement(ref inside);
                    return null;
                }, CancellationToken.None))
                .ToArray();

            await Task.WhenAll(writes);

            Assert.Equal(1, maxInside);
        }

        [Fact]
        public async Task NestedWrite_InsideAFlushedWrite_CommitsWithoutOpeningASecondWriter()
        {
            // Red today: the outer write has taken SQLite's write lock by flushing, so a nested ExecuteAsync that opens its
            // own connection waits the full Default Timeout and fails. A nested write must reuse the ambient context.
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                await context.SaveChangesAsync(CancellationToken.None);

                _ = await _unitOfWork.ExecuteAsync<object?>(async nested =>
                {
                    ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await nested.ProviderAccounts.AddAsync(account, CancellationToken.None);
                    return null;
                }, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"nested write took {stopwatch.Elapsed.TotalSeconds:F1}s, so it waited on the outer write");

            await using VideoForensicsDbContext ctx = _factory.CreateDbContext();
            Assert.True(await ctx.Users.AnyAsync(u => u.Id == userId));
            Assert.True(await ctx.ProviderAccounts.AnyAsync(a => a.Id == accountId));
        }

        [Fact]
        public async Task ReadInsideAWrite_SeesUncommittedChanges()
        {
            // Red today: ReadAsync opens a new connection, so it cannot see rows the open write has flushed but not committed.
            // Inside a write, reads must use the ambient write context. This is a deliberate behaviour change.
            var userId = Guid.NewGuid();
            bool seen = false;

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                await context.SaveChangesAsync(CancellationToken.None);

                seen = await _unitOfWork.ReadAsync(async read => await read.Users.GetAsync(userId, CancellationToken.None) is not null, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Assert.True(seen);
        }

        /// <summary>Creates a fresh context, and so a fresh pooled connection, per call, as the application does.</summary>
        private sealed class PooledDbContextFactory : IDbContextFactory<VideoForensicsDbContext>
        {
            private readonly DbContextOptions<VideoForensicsDbContext> _options;

            public PooledDbContextFactory(DbContextOptions<VideoForensicsDbContext> options)
            {
                _options = options;
            }

            public VideoForensicsDbContext CreateDbContext()
            {
                return new(_options);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
