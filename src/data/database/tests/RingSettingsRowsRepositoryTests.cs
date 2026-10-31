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
    /// <summary>Round-trips the RingDeviceSettings and DeviceGeneralSettings rows (replace-on-sync, cascade on device delete) and the ota_status alert row through SQLite.</summary>
    public class RingSettingsRowsRepositoryTests : IAsyncLifetime
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

        private static DeviceSettingsChildren Children(RingDeviceSettings? ring, DeviceGeneralSettings? general)
        {
            return new DeviceSettingsChildren([], [], [], RingSettings: ring, GeneralSettings: general);
        }

        private static RingDeviceSettings Ring(bool stark, int? lite24x7Ttl = null)
        {
            return new RingDeviceSettings { StarkEnabled = stark, Lite24x7FootageTtl = lite24x7Ttl, PirZone6Sensitivity = 2, RingCreatedAt = new DateTime(2021, 8, 29, 23, 7, 38, DateTimeKind.Utc) };
        }

        private static DeviceGeneralSettings General(int voiceVolume)
        {
            return new DeviceGeneralSettings { VoiceVolume = voiceVolume, StatusLedEnabled = true };
        }

        [Fact]
        public async Task UpsertAsync_RingAndGeneralSettings_WritesOneRowPerDevice()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(true, 336), General(11)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceSettings ring = await db.RingDeviceSettings.SingleAsync(r => r.DeviceId == _device.Id);
            Assert.True(ring.StarkEnabled);
            Assert.Equal(336, ring.Lite24x7FootageTtl);
            Assert.Equal(2, ring.PirZone6Sensitivity);
            Assert.Equal(new DateTime(2021, 8, 29, 23, 7, 38, DateTimeKind.Utc), ring.RingCreatedAt);
            Assert.NotEqual(default, ring.UpdatedAtUtc);
            Assert.Null(ring.EnableVod);
            DeviceGeneralSettings general = await db.DeviceGeneralSettings.SingleAsync(g => g.DeviceId == _device.Id);
            Assert.Equal(11, general.VoiceVolume);
            Assert.True(general.StatusLedEnabled);
            Assert.Null(general.FloodlightBrightness);
        }

        [Fact]
        public async Task UpsertAsync_SecondCall_ReplacesRowsWithoutDuplicates()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(true), General(11)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(false, 24), General(5)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.RingDeviceSettings.CountAsync());
            RingDeviceSettings ring = await db.RingDeviceSettings.SingleAsync();
            Assert.False(ring.StarkEnabled);
            Assert.Equal(24, ring.Lite24x7FootageTtl);
            Assert.Equal(1, await db.DeviceGeneralSettings.CountAsync());
            Assert.Equal(5, (await db.DeviceGeneralSettings.SingleAsync()).VoiceVolume);
        }

        [Fact]
        public async Task UpsertAsync_NullRows_LeaveStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(true, 336), General(11)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.True((await db.RingDeviceSettings.SingleAsync()).StarkEnabled);
            Assert.Equal(11, (await db.DeviceGeneralSettings.SingleAsync()).VoiceVolume);
        }

        [Fact]
        public async Task UpsertAsync_EmptyRows_WriteRowsOfNulls()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(true, 336), General(11)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(new RingDeviceSettings(), new DeviceGeneralSettings()));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceSettings ring = await db.RingDeviceSettings.SingleAsync();
            Assert.Null(ring.StarkEnabled);
            Assert.Null(ring.Lite24x7FootageTtl);
            Assert.Null(ring.RingCreatedAt);
            DeviceGeneralSettings general = await db.DeviceGeneralSettings.SingleAsync();
            Assert.Null(general.VoiceVolume);
        }

        [Fact]
        public async Task UpsertAsync_OtaStatusAlert_StoredAsAlertTypeOtaStatus()
        {
            var alerts = new List<DeviceAlerts>
            {
                new() { AlertType = "connection", AlertValue = "online" },
                new() { AlertType = "ota_status", AlertValue = "successful" }
            };
            await _repository.UpsertAsync(_device.Id, null, null, null, alerts, CancellationToken.None);
            await _repository.UpsertAsync(_device.Id, null, null, null, new List<DeviceAlerts> { new() { AlertType = "ota_status", AlertValue = "failed" } }, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(2, await db.DeviceAlerts.CountAsync(a => a.DeviceId == _device.Id));
            Assert.Equal("failed", (await db.DeviceAlerts.SingleAsync(a => a.DeviceId == _device.Id && a.AlertType == "ota_status")).AlertValue);
        }

        [Fact]
        public async Task DeleteDevice_CascadesToRingAndGeneralSettingsRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Ring(true), General(11)));

            await using (VideoForensicsDbContext write = await _fixture.Factory.CreateDbContextAsync())
            {
                Device d = await write.Devices.SingleAsync(x => x.Id == _device.Id);
                _ = write.Devices.Remove(d);
                _ = await write.SaveChangesAsync();
            }

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.RingDeviceSettings.CountAsync());
            Assert.Equal(0, await db.DeviceGeneralSettings.CountAsync());
        }
    }
}
