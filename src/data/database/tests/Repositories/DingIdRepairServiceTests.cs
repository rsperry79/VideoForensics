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
    /// The one-time repair that moves truncated Events and DownloadEvents rows onto their matched string ding ids.
    /// A dry run reports counts only. Apply takes a backup first, then changes matched rows in one transaction.
    /// </summary>
    public class DingIdRepairServiceTests : IAsyncLifetime
    {
        private const long DoorbotId = 86401464;
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
            _ = await SeedHistoryAsync("1234567890123456");
            _ = await SeedHistoryAsync("9999999999999999");
            _ = await SeedHistoryAsync("1111111111111111");
            await SeedLocationAsync("1234567890123456789");
            await SeedLocationAsync("1111111111111111111");
            await SeedLocationAsync("1111111111111111222");
            Guid eventId = await SeedEventAsync(device.Id, "1234567890123456");

            int backupCalls = 0;
            DingIdRepairReport report = await RunAsync(false, _ =>
            {
                backupCalls++;
                return Task.CompletedTask;
            });

            Assert.False(report.Applied);
            Assert.Equal(new DingIdMatchCounts(1, 1, 1), report.DoorbotHistory);
            Assert.Equal(0, backupCalls);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("1234567890123456", (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_RepairsTruncatedEventsAndLinks()
        {
            Device device = await SeedDeviceAsync();
            Guid historyId = await SeedHistoryAsync("1234567890123456");
            await SeedLocationAsync("1234567890123456789");
            Guid eventId = await SeedEventAsync(device.Id, "1234567890123456");
            await SeedDownloadAsync(device.Id, "1234567890123456");

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.True(report.Applied);
            Assert.Equal(1, report.EventsRepaired);
            Assert.Equal(1, report.DownloadEventsRepaired);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("1234567890123456789", (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
            Assert.Equal("1234567890123456789", (await db.DownloadEvents.SingleAsync()).ProviderEventId);
            Assert.Equal(eventId, (await db.RingDoorbotHistoryEvents.SingleAsync(e => e.Id == historyId)).LinkedEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_AmbiguousRowIsLeftAlone()
        {
            Device device = await SeedDeviceAsync();
            _ = await SeedHistoryAsync("1111111111111111");
            await SeedLocationAsync("1111111111111111111");
            await SeedLocationAsync("1111111111111111222");
            Guid eventId = await SeedEventAsync(device.Id, "1111111111111111");

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.Equal(1, report.DoorbotHistory.Ambiguous);
            Assert.Equal(0, report.EventsRepaired);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("1111111111111111", (await db.Events.SingleAsync(e => e.Id == eventId)).ProviderEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_CollisionWithExistingRow_IsSkippedAndCounted()
        {
            Device device = await SeedDeviceAsync();
            _ = await SeedHistoryAsync("1234567890123456");
            await SeedLocationAsync("1234567890123456789");
            Guid truncatedEventId = await SeedEventAsync(device.Id, "1234567890123456");
            _ = await SeedEventAsync(device.Id, "1234567890123456789");

            DingIdRepairReport report = await RunAsync(true, _ => Task.CompletedTask);

            Assert.Equal(1, report.EventsSkipped);
            Assert.Equal(0, report.EventsRepaired);
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal("1234567890123456", (await db.Events.SingleAsync(e => e.Id == truncatedEventId)).ProviderEventId);
        }

        [Fact]
        public async Task DingIdRepairService_Apply_CallsBackupBeforeChanges()
        {
            Device device = await SeedDeviceAsync();
            _ = await SeedHistoryAsync("1234567890123456");
            await SeedLocationAsync("1234567890123456789");
            _ = await SeedEventAsync(device.Id, "1234567890123456");

            int backupCalls = 0;
            string? eventIdSeenByBackup = null;
            _ = await RunAsync(true, async ct =>
            {
                backupCalls++;
                await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
                eventIdSeenByBackup = (await db.Events.SingleAsync(ct)).ProviderEventId;
            });

            Assert.Equal(1, backupCalls);
            Assert.Equal("1234567890123456", eventIdSeenByBackup);
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
