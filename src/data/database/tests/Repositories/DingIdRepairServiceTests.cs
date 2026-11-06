using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Matching;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// The repair links each exactly matched doorbot history row to the Events row that already holds its string ding
    /// id on the same device. It changes no other data. A dry run reports counts only. Apply takes a backup first and
    /// links in one transaction.
    /// </summary>
    public class DingIdRepairServiceTests : IAsyncLifetime
    {
        private const long DoorbotId = 86401464;
        private const string DingId = "1234567890123456789";
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DingIdRepairService_DryRun_ReportsCountsAndChangesNothing()
        {
            Device device = await SeedDeviceAsync();
            _ = await SeedHistoryAsync(DingId);
            _ = await SeedHistoryAsync("9999999999999999999");
            _ = await SeedHistoryAsync("1111111111111111111");
            await SeedLocationAsync(DingId);
            await SeedLocationAsync("1111111111111111111");
            Guid eventId = await SeedEventAsync(device.Id, DingId);

            int backupCalls = 0;
            DingIdRepairReport report = await RunAsync(false, _ =>
            {
                backupCalls++;
                return Task.CompletedTask;
            });

            Assert.False(report.Applied);
            Assert.Equal(new DingIdMatchCounts(2, 1, 0), report.DoorbotHistory);
            Assert.Equal(0, report.EventsLinked);
            Assert.Equal(0, backupCalls);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(DingId, (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Empty(await db.RingDoorbotHistoryEvents.Where(h => h.LinkedEventId != null).ToListAsync());
        }

        [Fact]
        public async Task DingIdRepairService_Apply_LinksExactMatchAndChangesNothingElse()
        {
            Device device = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync(DingId);
            await SeedLocationAsync(DingId);
            Guid eventId = await SeedEventAsync(device.Id, DingId);
            await SeedDownloadAsync(device.Id, DingId);

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.True(report.Applied);
            Assert.Equal(1, report.EventsLinked);
            Assert.Equal(0, report.NoEventRow);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(DingId, (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Equal(DingId, (await db.DownloadEvents.SingleAsync()).ProviderEventId);
            Assert.Equal(eventId, (await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_MatchedRowWithoutEventRow_CountsNoEventRowAndCreatesNothing()
        {
            _ = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync(DingId);
            await SeedLocationAsync(DingId);

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.Equal(1, report.DoorbotHistory.Matched);
            Assert.Equal(0, report.EventsLinked);
            Assert.Equal(1, report.NoEventRow);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Empty(await db.Events.ToListAsync());
            Assert.Null((await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_PrefixOnlyCandidate_IsUnmatchedAndNotLinked()
        {
            Device device = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync("1234567890123456");
            await SeedLocationAsync(DingId);
            Guid eventId = await SeedEventAsync(device.Id, DingId);

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.Equal(new DingIdMatchCounts(0, 1, 0), report.DoorbotHistory);
            Assert.Equal(0, report.EventsLinked);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(DingId, (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Null((await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_CallsBackupBeforeChanges()
        {
            Device device = await SeedDeviceAsync();
            _ = await SeedHistoryAsync(DingId);
            await SeedLocationAsync(DingId);
            _ = await SeedEventAsync(device.Id, DingId);

            int backupCalls = 0;
            string? eventLinkSeenByBackup = "not-run";
            _ = await RunAsync(true, async ct =>
            {
                backupCalls++;
                await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
                eventLinkSeenByBackup = (await db.RingDoorbotHistoryEvents.SingleAsync(ct)).LinkedEventId?.ToString();
            });

            Assert.Equal(1, backupCalls);
            Assert.Null(eventLinkSeenByBackup);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_HoldLostBeforeCommit_RollsBackAndAborts()
        {
            Device device = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync(DingId);
            await SeedLocationAsync(DingId);
            Guid eventId = await SeedEventAsync(device.Id, DingId);
            await SeedDownloadAsync(device.Id, DingId);

            int confirmCalls = 0;
            var service = new DingIdRepairService();
            DingIdRepairReport report = await service.RunAsync(
                _fixture.Factory,
                apply: true,
                backup: _ => Task.CompletedTask,
                ct: CancellationToken.None,
                confirmHold: _ =>
                {
                    confirmCalls++;
                    return Task.FromResult(false);
                });

            Assert.Equal(1, confirmCalls);
            Assert.True(report.Aborted);
            Assert.False(report.Applied);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(DingId, (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Equal(DingId, (await db.DownloadEvents.SingleAsync()).ProviderEventId);
            Assert.Null((await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        [Fact]
        public async Task DingIdRepairService_BeforeWritesFails_AbortsWithoutChanges()
        {
            Device device = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync(DingId);
            await SeedLocationAsync(DingId);
            Guid eventId = await SeedEventAsync(device.Id, DingId);

            int beforeWritesCalls = 0;
            var service = new DingIdRepairService();
            DingIdRepairReport report = await service.RunAsync(
                _fixture.Factory,
                apply: true,
                backup: _ => Task.CompletedTask,
                ct: CancellationToken.None,
                beforeWrites: _ =>
                {
                    beforeWritesCalls++;
                    return Task.FromResult(false);
                });

            Assert.Equal(1, beforeWritesCalls);
            Assert.True(report.Aborted);
            Assert.False(report.Applied);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(DingId, (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Null((await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        private async Task<DingIdRepairReport> RunAsync(bool apply, Func<CancellationToken, Task> backup)
        {
            var service = new DingIdRepairService();
            return await service.RunAsync(_fixture.Factory, apply, backup, CancellationToken.None);
        }

        private async Task<Device> SeedDeviceAsync()
        {
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: DoorbotId.ToString());
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Devices.Add(device);
            _ = await db.SaveChangesAsync();
            return device;
        }

        private async Task<Guid> SeedHistoryAsync(string historyId)
        {
            var row = new RingDoorbotHistoryEvent { Id = Guid.NewGuid(), ProviderEventId = historyId };
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingDoorbotHistoryEvents.Add(row);
            _ = db.RingEventDoorbots.Add(new RingEventDoorbot
            {
                Id = Guid.NewGuid(),
                Source = RingEventSources.DoorbotHistory,
                SourceRowId = row.Id,
                DoorbotId = DoorbotId,
            });
            _ = await db.SaveChangesAsync();
            return row.Id;
        }

        private async Task SeedLocationAsync(string dingIdString)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingLocationEvents.Add(new RingLocationEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = "location-1",
                ProviderEventId = dingIdString,
                DoorbotId = DoorbotId,
                DingIdString = dingIdString,
            });
            _ = await db.SaveChangesAsync();
        }

        private async Task<Guid> SeedEventAsync(Guid deviceId, string providerEventId)
        {
            var evt = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = providerEventId,
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow,
            };
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Events.Add(evt);
            _ = await db.SaveChangesAsync();
            return evt.Id;
        }

        private async Task SeedDownloadAsync(Guid deviceId, string providerEventId)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.DownloadEvents.Add(new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = providerEventId,
                AppVersion = "test",
                EventOccurredAtUtc = DateTime.UtcNow,
                DownloadStartedUtc = DateTime.UtcNow,
            });
            _ = await db.SaveChangesAsync();
        }
    }
}
