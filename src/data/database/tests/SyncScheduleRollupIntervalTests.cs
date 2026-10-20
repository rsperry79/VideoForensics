using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The snapshot roll-up interval: default 15 minutes, saved in minutes, minimum 1.</summary>
    public class SyncScheduleRollupIntervalTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private SyncScheduleRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new SyncScheduleRepository(_fixture.Factory, NullLogger<SyncScheduleRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public void NewSchedule_DefaultsRollupToFifteenMinutes()
        {
            Assert.Equal(15, SyncSchedule.CreateDefault(Guid.NewGuid()).SnapshotRollupIntervalMinutes);
        }

        [Fact]
        public async Task UpsertAsync_RollupBelowMinimum_Throws()
        {
            Guid account = Guid.NewGuid();
            SyncSchedule schedule = SyncSchedule.CreateDefault(account);
            schedule.SnapshotRollupIntervalMinutes = 0;

            await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpsertAsync(schedule, CancellationToken.None));
        }

        [Fact]
        public async Task UpsertAsync_RollupEdited_PersistsNewInterval()
        {
            Guid account = Guid.NewGuid();
            SyncSchedule schedule = SyncSchedule.CreateDefault(account);
            await _repository.UpsertAsync(schedule, CancellationToken.None);

            schedule.SnapshotRollupIntervalMinutes = 30;
            await _repository.UpsertAsync(schedule, CancellationToken.None);

            SyncSchedule? saved = await _repository.GetByProviderAccountIdAsync(account, CancellationToken.None);
            Assert.Equal(30, saved!.SnapshotRollupIntervalMinutes);
        }
    }
}
