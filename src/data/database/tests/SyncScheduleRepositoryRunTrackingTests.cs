using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class SyncScheduleRepositoryRunTrackingTests : IAsyncLifetime
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

        private static SyncSchedule Build(Guid accountId, bool enabled = true, int eventMinutes = 30, int snapshotMinutes = 45, bool advanced = false)
        {
            return new SyncSchedule
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = accountId,
                IsEnabled = enabled,
                EventPollIntervalMinutes = eventMinutes,
                SnapshotRssiIntervalMinutes = snapshotMinutes,
                UseAdvancedJammingSchedule = advanced
            };
        }

        [Fact]
        public async Task ListEnabledAsync_MixedSchedules_ReturnsOnlyEnabledOrderedByAccountId()
        {
            Guid a = new("00000000-0000-0000-0000-000000000003");
            Guid b = new("00000000-0000-0000-0000-000000000001");
            Guid c = new("00000000-0000-0000-0000-000000000002");
            await _repository.UpsertAsync(Build(a), CancellationToken.None);
            await _repository.UpsertAsync(Build(b), CancellationToken.None);
            await _repository.UpsertAsync(Build(c, enabled: false), CancellationToken.None);

            IReadOnlyList<SyncSchedule> result = await _repository.ListEnabledAsync(CancellationToken.None);

            Assert.Equal([b, a], result.Select(s => s.ProviderAccountId).ToArray());
        }

        [Fact]
        public async Task ListEnabledAsync_NoSchedules_ReturnsEmpty()
        {
            IReadOnlyList<SyncSchedule> result = await _repository.ListEnabledAsync(CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task ListEnabledAsync_ScheduleWithWindows_IncludesJammingWindows()
        {
            SyncSchedule schedule = Build(Guid.NewGuid(), advanced: true);
            schedule.JammingWindows.Add(new JammingScheduleWindow
            {
                Id = Guid.NewGuid(),
                DayOfWeek = 2,
                StartMinuteOfDay = 60,
                EndMinuteOfDay = 120,
                SnapshotRssiIntervalMinutes = 5
            });
            await _repository.UpsertAsync(schedule, CancellationToken.None);

            IReadOnlyList<SyncSchedule> result = await _repository.ListEnabledAsync(CancellationToken.None);

            JammingScheduleWindow window = Assert.Single(Assert.Single(result).JammingWindows);
            Assert.Equal(2, window.DayOfWeek);
            Assert.Equal(5, window.SnapshotRssiIntervalMinutes);
        }

        [Fact]
        public async Task RecordEventRunAsync_ExistingRow_UpdatesOnlyEventRunFields()
        {
            Guid account = Guid.NewGuid();
            await _repository.UpsertAsync(Build(account, eventMinutes: 30, snapshotMinutes: 45, advanced: true), CancellationToken.None);
            var ran = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
            DateTime next = ran.AddMinutes(30);

            await _repository.RecordEventRunAsync(account, ran, next, CancellationToken.None);

            SyncSchedule? row = await _repository.GetByProviderAccountIdAsync(account, CancellationToken.None);
            Assert.NotNull(row);
            Assert.Equal(ran, row.EventLastRunUtc);
            Assert.Equal(next, row.EventNextRunUtc);
            Assert.Null(row.SnapshotLastRunUtc);
            Assert.Null(row.SnapshotNextRunUtc);
            Assert.Equal(30, row.EventPollIntervalMinutes);
            Assert.Equal(45, row.SnapshotRssiIntervalMinutes);
            Assert.True(row.IsEnabled);
            Assert.True(row.UseAdvancedJammingSchedule);
        }

        [Fact]
        public async Task RecordEventRunAsync_IntervalsEditedAfterLoad_DoesNotOverwriteEdits()
        {
            Guid account = Guid.NewGuid();
            await _repository.UpsertAsync(Build(account, eventMinutes: 30), CancellationToken.None);
            // User edits the schedule while a run is in flight.
            await _repository.UpsertAsync(Build(account, enabled: false, eventMinutes: 99, snapshotMinutes: 77), CancellationToken.None);

            await _repository.RecordEventRunAsync(account, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), CancellationToken.None);

            SyncSchedule? row = await _repository.GetByProviderAccountIdAsync(account, CancellationToken.None);
            Assert.NotNull(row);
            Assert.False(row.IsEnabled);
            Assert.Equal(99, row.EventPollIntervalMinutes);
            Assert.Equal(77, row.SnapshotRssiIntervalMinutes);
        }

        [Fact]
        public async Task RecordSnapshotRunAsync_ExistingRow_UpdatesOnlySnapshotRunFields()
        {
            Guid account = Guid.NewGuid();
            await _repository.UpsertAsync(Build(account), CancellationToken.None);
            var ran = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);
            DateTime next = ran.AddMinutes(45);

            await _repository.RecordSnapshotRunAsync(account, ran, next, CancellationToken.None);

            SyncSchedule? row = await _repository.GetByProviderAccountIdAsync(account, CancellationToken.None);
            Assert.NotNull(row);
            Assert.Equal(ran, row.SnapshotLastRunUtc);
            Assert.Equal(next, row.SnapshotNextRunUtc);
            Assert.Null(row.EventLastRunUtc);
            Assert.Null(row.EventNextRunUtc);
            Assert.Equal(30, row.EventPollIntervalMinutes);
            Assert.Equal(45, row.SnapshotRssiIntervalMinutes);
            Assert.True(row.IsEnabled);
        }

        [Fact]
        public async Task RecordEventRunAsync_MissingRow_IsNoOp()
        {
            await _repository.RecordEventRunAsync(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), CancellationToken.None);

            Assert.Empty(await _repository.ListEnabledAsync(CancellationToken.None));
        }

        [Fact]
        public async Task RecordSnapshotRunAsync_MissingRow_IsNoOp()
        {
            await _repository.RecordSnapshotRunAsync(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), CancellationToken.None);

            Assert.Empty(await _repository.ListEnabledAsync(CancellationToken.None));
        }
    }
}