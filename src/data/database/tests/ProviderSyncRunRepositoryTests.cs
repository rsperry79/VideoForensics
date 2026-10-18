using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class ProviderSyncRunRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ProviderSyncRunRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new ProviderSyncRunRepository(_fixture.Factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static ProviderSyncRun Build(Guid accountId, DateTime startedUtc, bool success = true, string? error = null)
        {
            return new ProviderSyncRun
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = accountId,
                StartedUtc = startedUtc,
                CompletedUtc = startedUtc.AddSeconds(5),
                Success = success,
                ErrorMessage = error
            };
        }

        [Fact]
        public async Task ListByProviderAccountIdAsync_AddedOutOfOrder_ReturnsNewestStartedFirst()
        {
            Guid account = Guid.NewGuid();
            var t1 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
            var t3 = new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);
            await _repository.AddAsync(Build(account, t2), CancellationToken.None);
            await _repository.AddAsync(Build(account, t3), CancellationToken.None);
            await _repository.AddAsync(Build(account, t1), CancellationToken.None);

            IReadOnlyList<ProviderSyncRun> result = await _repository.ListByProviderAccountIdAsync(account, CancellationToken.None);

            Assert.Equal([t3, t2, t1], result.Select(r => r.StartedUtc).ToArray());
        }

        [Fact]
        public async Task ListByProviderAccountIdAsync_RunsForOtherAccounts_ExcludesThem()
        {
            Guid account = Guid.NewGuid();
            Guid other = Guid.NewGuid();
            var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
            await _repository.AddAsync(Build(account, now), CancellationToken.None);
            await _repository.AddAsync(Build(other, now.AddMinutes(1)), CancellationToken.None);

            IReadOnlyList<ProviderSyncRun> result = await _repository.ListByProviderAccountIdAsync(account, CancellationToken.None);

            ProviderSyncRun run = Assert.Single(result);
            Assert.Equal(account, run.ProviderAccountId);
        }

        [Fact]
        public async Task AddAsync_FailedRun_RoundTripsErrorMessageAndCompletion()
        {
            Guid account = Guid.NewGuid();
            ProviderSyncRun run = Build(account, new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc), success: false, error: "refresh token rejected");
            await _repository.AddAsync(run, CancellationToken.None);

            ProviderSyncRun? stored = await _repository.GetAsync(run.Id, CancellationToken.None);

            Assert.NotNull(stored);
            Assert.False(stored.Success);
            Assert.Equal("refresh token rejected", stored.ErrorMessage);
            Assert.Equal(run.CompletedUtc, stored.CompletedUtc);
        }

        [Fact]
        public async Task AddAsync_RunWithSyncTypeAndCounts_PersistsThem()
        {
            ProviderSyncRun run = Build(Guid.NewGuid(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));
            run.SyncType = "Incremental";
            run.DevicesSynced = 3;
            run.LocationsSynced = 1;
            run.EventsSynced = 42;
            run.ConfigAreasSynced = 6;
            run.FeedsFailed = 2;
            run.SnapshotsSynced = 4;
            await _repository.AddAsync(run, CancellationToken.None);

            ProviderSyncRun? stored = await _repository.GetAsync(run.Id, CancellationToken.None);

            Assert.NotNull(stored);
            Assert.Equal("Incremental", stored.SyncType);
            Assert.Equal(3, stored.DevicesSynced);
            Assert.Equal(1, stored.LocationsSynced);
            Assert.Equal(42, stored.EventsSynced);
            Assert.Equal(6, stored.ConfigAreasSynced);
            Assert.Equal(2, stored.FeedsFailed);
            Assert.Equal(4, stored.SnapshotsSynced);
        }

        [Fact]
        public async Task AddAsync_RunWithoutSyncType_StoresUnknownAndNullCounts()
        {
            ProviderSyncRun run = Build(Guid.NewGuid(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));
            await _repository.AddAsync(run, CancellationToken.None);

            ProviderSyncRun? stored = await _repository.GetAsync(run.Id, CancellationToken.None);

            Assert.NotNull(stored);
            // The column default fills in "Unknown" for runs recorded without a type.
            Assert.Equal("Unknown", stored.SyncType);
            Assert.Null(stored.EventsSynced);
        }

        [Fact]
        public async Task GetAsync_UnknownId_ReturnsNull()
        {
            ProviderSyncRun? result = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
        }
    }
}
