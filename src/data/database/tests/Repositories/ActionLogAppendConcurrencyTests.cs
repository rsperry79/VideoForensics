using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Concurrent standalone appends on a file-backed, pooled SQLite database, as production runs. The shared in-memory
    /// fixture uses one connection, which serialises every append and so hides the race.
    /// </summary>
    public class ActionLogAppendConcurrencyTests : IAsyncLifetime
    {
        private string _databasePath = null!;
        private IDbContextFactory<VideoForensicsDbContext> _factory = null!;
        private ActionLogRepository _repository = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"actionlog-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite($"Data Source={_databasePath};Pooling=true;Default Timeout=5")
                .Options;
            _factory = new PooledDbContextFactory(options);

            await using VideoForensicsDbContext db = _factory.CreateDbContext();
            _ = await db.Database.EnsureCreatedAsync();

            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ActionLogRepository(_factory, loggerFactory.CreateLogger<ActionLogRepository>());
            _unitOfWork = new UnitOfWork(_factory, new ServiceCollection().AddLogging().BuildServiceProvider(), loggerFactory.CreateLogger<UnitOfWork>());
        }

        public ValueTask DisposeAsync()
        {
            // Pooled connections are released when the process clears the pool; delete the file once they are gone.
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
        public async Task ActionLogRepository_ConcurrentAppendsOnPooledDatabase_KeepChainValid()
        {
            Task<ActionLogEntry>[] appends = Enumerable.Range(0, 20)
                .Select(i => _repository.AppendAsync($"Actor{i}", ActorType.Human, $"Act{i}", "Ent", Guid.NewGuid(), null, CancellationToken.None))
                .ToArray();

            ActionLogEntry[] entries = await Task.WhenAll(appends);
            ChainIntegrityResult result = await _repository.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.Equal(20, entries.Select(e => e.Id).Distinct().Count());
            Assert.True(result.IsValid, $"chain broke: {result.Break} at {result.FailedEntryId} after {result.EntriesChecked}");
            Assert.Equal(20, result.EntriesChecked);
        }

        [Fact]
        public async Task UnitOfWork_StandaloneAppendInsideUnit_JoinsUnitAndRecordsOnce()
        {
            // The standalone append must write on the unit's connection (a second one would wait on the unit's write lock)
            // and each append must produce exactly one row.
            Task unit = _unitOfWork.ExecuteAsync(async context =>
            {
                _ = await _repository.AppendAsync("Inner", ActorType.System, "InnerAction", "Ent", null, null, CancellationToken.None);
                _ = await context.ActionLog.AppendAsync("Outer", ActorType.System, "OuterAction", "Ent", null, null, CancellationToken.None);
                return true;
            }, CancellationToken.None);

            Task finished = await Task.WhenAny(unit, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(unit, finished);
            await unit;
            IReadOnlyList<ActionLogEntry> rows = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows.Select(r => r.Action).Distinct().Count());
            Assert.True((await _repository.VerifyChainIntegrityAsync(CancellationToken.None)).IsValid);
        }

        [Fact]
        public async Task ActionLogAppends_MixedUnitsAndStandaloneConcurrently_KeepChainValid()
        {
            // Units of work and standalone appends interleave; every append must chain to the tail the previous one wrote.
            var writes = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int n = i;
                writes.Add(_unitOfWork.ExecuteAsync(async context =>
                {
                    _ = await context.ActionLog.AppendAsync("Unit", ActorType.System, $"U{n}a", "Ent", null, null, CancellationToken.None);
                    _ = await context.ActionLog.AppendAsync("Unit", ActorType.System, $"U{n}b", "Ent", null, null, CancellationToken.None);
                    return true;
                }, CancellationToken.None));
                writes.Add(_repository.AppendAsync("Standalone", ActorType.Human, $"S{n}", "Ent", null, null, CancellationToken.None));
            }

            await Task.WhenAll(writes);
            ChainIntegrityResult result = await _repository.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.True(result.IsValid, $"chain broke: {result.Break} at {result.FailedEntryId} after {result.EntriesChecked}");
            Assert.Equal(15, result.EntriesChecked);
        }

        /// <summary>Creates a fresh context, and so a fresh pooled connection, per call, as the application does.</summary>
        private sealed class PooledDbContextFactory : IDbContextFactory<VideoForensicsDbContext>
        {
            private readonly DbContextOptions<VideoForensicsDbContext> _options;

            public PooledDbContextFactory(DbContextOptions<VideoForensicsDbContext> options)
            {
                _options = options;
            }

            public VideoForensicsDbContext CreateDbContext() => new(_options);
        }
    }
}
