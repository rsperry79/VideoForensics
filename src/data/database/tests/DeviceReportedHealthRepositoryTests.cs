using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Round-trips the device-reported health row and its battery rows (replace-on-sync) through SQLite.</summary>
    public class DeviceReportedHealthRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            _repository = new DeviceDetailsRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceDetailsRepository>());
            var locationRepository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
            var deviceRepository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
            Location location = TestDataBuilder.BuildLocation();
            await locationRepository.AddAsync(location, CancellationToken.None);
            _device = TestDataBuilder.BuildDevice(locationId: location.Id);
            await deviceRepository.AddAsync(_device, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static DeviceSettingsChildren Children(DeviceReportedHealth? health, params DeviceReportedBattery[] batteries)
        {
            return new DeviceSettingsChildren([], [], [], ReportedHealth: health, ReportedBatteries: health == null ? null : batteries,
                RingHealthExtras: health == null ? null : new RingDeviceHealthExtras { TransformerVoltage = health.Connected == true ? 17.55m : 12.5m, WifiIsRingNetwork = false });
        }

        private static DeviceReportedHealth Health(bool connected, long updateTime)
        {
            return new DeviceReportedHealth { Connected = connected, LastUpdateTime = updateTime, Rssi = -60.5, StatusTime = 58766634686231L };
        }

        private static DeviceReportedBattery Battery(int position, int number, int? percent)
        {
            return new DeviceReportedBattery { Position = position, BatteryNumber = number, Percentage = percent, Charging = false, Temp = 2921.0, FwVersion = "0.9" };
        }

        [Fact]
        public async Task UpsertAsync_ReportedHealth_WritesRowAndBatteries()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Health(true, 1700000000), Battery(0, 1, 88), Battery(1, 2, null)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceReportedHealth stored = await db.DeviceReportedHealths.SingleAsync(h => h.DeviceId == _device.Id);
            Assert.True(stored.Connected);
            Assert.Equal(1700000000L, stored.LastUpdateTime);
            Assert.Equal(-60.5, stored.Rssi);
            Assert.Equal(17.55m, (await db.RingDeviceHealthExtras.SingleAsync(x => x.DeviceId == _device.Id)).TransformerVoltage);
            Assert.Equal(58766634686231L, stored.StatusTime);
            Assert.NotEqual(default, stored.UpdatedAtUtc);
            List<DeviceReportedBattery> batteries = await db.DeviceReportedBatteries.Where(b => b.DeviceId == _device.Id).OrderBy(b => b.Position).ToListAsync();
            Assert.Equal([1, 2], batteries.Select(b => b.BatteryNumber));
            Assert.Equal(88, batteries[0].Percentage);
            Assert.Null(batteries[1].Percentage);
            Assert.Equal("0.9", batteries[0].FwVersion);
        }

        [Fact]
        public async Task UpsertAsync_ReportedHealthSecondCall_ReplacesRowAndBatteriesWithoutDuplicates()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Health(true, 1), Battery(0, 1, 50), Battery(1, 2, 60)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Health(false, 2), Battery(0, 1, 40)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceReportedHealth stored = await db.DeviceReportedHealths.SingleAsync(h => h.DeviceId == _device.Id);
            Assert.False(stored.Connected);
            Assert.Equal(2L, stored.LastUpdateTime);
            Assert.Equal(12.5m, (await db.RingDeviceHealthExtras.SingleAsync(x => x.DeviceId == _device.Id)).TransformerVoltage);
            DeviceReportedBattery battery = await db.DeviceReportedBatteries.SingleAsync(b => b.DeviceId == _device.Id);
            Assert.Equal(40, battery.Percentage);
        }

        [Fact]
        public async Task UpsertAsync_NullReportedHealth_LeavesStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Health(true, 1), Battery(0, 1, 50)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.DeviceReportedHealths.CountAsync());
            Assert.Equal(1, await db.DeviceReportedBatteries.CountAsync());
            Assert.Equal(1, await db.RingDeviceHealthExtras.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_HealthWithNoBatteries_ClearsStoredBatteries()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Health(true, 1), Battery(0, 1, 50)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Health(true, 2)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.DeviceReportedHealths.CountAsync());
            Assert.Equal(0, await db.DeviceReportedBatteries.CountAsync());
        }

        [Fact]
        public async Task SaveChanges_DeviceAgnosticColumns_RoundTrip()
        {
            await using (VideoForensicsDbContext write = await _fixture.Factory.CreateDbContextAsync())
            {
                Device d = await write.Devices.SingleAsync(x => x.Id == _device.Id);
                d.DeactivatedAtUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                d.SharedAtUtc = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
                d.LedSecondsRemaining = 9;
                _ = await write.SaveChangesAsync();
            }

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Device stored = await db.Devices.SingleAsync(x => x.Id == _device.Id);
            Assert.Equal(2025, stored.DeactivatedAtUtc!.Value.Year);
            Assert.Equal(2, stored.SharedAtUtc!.Value.Month);
            Assert.Equal(9, stored.LedSecondsRemaining);
        }

        [Fact]
        public async Task UpsertAsync_RingDetails_ReplacesRowAndNullLeavesItUntouched()
        {
            RingDeviceDetails First() => new() { SchemaId = "schema-a", IsSidewalkGateway = true, ExtPowerState = 1, EncryptionGroupId = "g1" };
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], [], RingDetails: First()));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                new DeviceSettingsChildren([], [], [], RingDetails: new RingDeviceDetails { SchemaId = "schema-b" }));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceDetails stored = await db.RingDeviceDetails.SingleAsync(r => r.DeviceId == _device.Id);
            Assert.Equal("schema-b", stored.SchemaId);
            Assert.Null(stored.IsSidewalkGateway);
            Assert.Null(stored.EncryptionGroupId);
            Assert.NotEqual(default, stored.UpdatedAtUtc);
        }
    }
}
