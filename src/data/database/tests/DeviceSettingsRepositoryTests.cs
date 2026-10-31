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
    /// <summary>Round-trips the device settings scalars, feature flags and settings child rows through SQLite.</summary>
    public class DeviceSettingsRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
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

        private static DeviceSettingsChildren Children(Guid deviceId, params string[] snooze)
        {
            var snoozeRows = snooze
                .Select((p, i) => new DeviceMotionSnoozePreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = i, Preset = p })
                .ToList();
            var zone = new DeviceAdvancedMotionZone
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ZoneKey = "zone1",
                Name = "driveway",
                State = 1,
                Vertices =
                [
                    new DeviceAdvancedMotionZoneVertex { Id = Guid.NewGuid(), Position = 1, X = 0.1, Y = 0.2 },
                    new DeviceAdvancedMotionZoneVertex { Id = Guid.NewGuid(), Position = 2, X = 0.3, Y = null }
                ]
            };
            return new DeviceSettingsChildren(
                MotionSnoozePresets: snoozeRows,
                LiveViewPresets: [new DeviceLiveViewPreset { Id = Guid.NewGuid(), DeviceId = deviceId, Position = 0, Preset = "night" }],
                AdvancedMotionZones: [zone]);
        }

        [Fact]
        public async Task UpsertAsync_SettingsChildren_WritesPresetsAndZoneVerticesInOrder()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, "1 hour", "until sunrise"));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            List<DeviceMotionSnoozePreset> snooze = await db.DeviceMotionSnoozePresets
                .Where(p => p.DeviceId == _device.Id).OrderBy(p => p.Position).ToListAsync();
            Assert.Equal(["1 hour", "until sunrise"], snooze.Select(p => p.Preset));
            DeviceLiveViewPreset live = await db.DeviceLiveViewPresets.SingleAsync(p => p.DeviceId == _device.Id);
            Assert.Equal("night", live.Preset);
            DeviceAdvancedMotionZone zone = await db.DeviceAdvancedMotionZones
                .Include(z => z.Vertices).SingleAsync(z => z.DeviceId == _device.Id);
            Assert.Equal("driveway", zone.Name);
            Assert.Equal(2, zone.Vertices.Count);
            Assert.Null(zone.Vertices.Single(v => v.Position == 2).Y);
        }

        [Fact]
        public async Task UpsertAsync_SettingsChildrenSecondCall_ReplacesPreviousRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, "1 hour", "until sunrise"));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(_device.Id, "2 hours"));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            string[] presets = await db.DeviceMotionSnoozePresets.Where(p => p.DeviceId == _device.Id).Select(p => p.Preset).ToArrayAsync();
            Assert.Equal(["2 hours"], presets);
            Assert.Equal(1, await db.DeviceAdvancedMotionZones.CountAsync(z => z.DeviceId == _device.Id));
            Assert.Equal(2, await db.DeviceAdvancedMotionZoneVertices.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_FeaturesWithVodAndRingtones_RoundTripsBothFlags()
        {
            var features = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = _device.Id, ShowVodSettings = true, RingtonesEnabled = false };

            await _repository.UpsertAsync(_device.Id, null, null, features, null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceFeatures stored = await db.DeviceFeatures.SingleAsync(f => f.DeviceId == _device.Id);
            Assert.True(stored.ShowVodSettings);
            Assert.False(stored.RingtonesEnabled);
        }

        [Fact]
        public async Task UpsertAsync_AlertWithConnectionValue_StoresValueOnAlertRow()
        {
            var alert = new DeviceAlerts { Id = Guid.NewGuid(), DeviceId = _device.Id, AlertType = "connection", AlertValue = "online" };

            await _repository.UpsertAsync(_device.Id, null, null, null, [alert], CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceAlerts stored = await db.DeviceAlerts.SingleAsync(a => a.DeviceId == _device.Id);
            Assert.Equal("online", stored.AlertValue);
        }

        [Fact]
        public async Task Device_RemainingScalars_PersistAndReadBack()
        {
            await using (VideoForensicsDbContext write = await _fixture.Factory.CreateDbContextAsync())
            {
                Device device = await write.Devices.SingleAsync(d => d.Id == _device.Id);
                device.RingType = "doorbot";
                device.RingRingId = "abc";
                device.BatteryLife2 = "55";
                device.BatteryVoltage = 12.5m;
                device.BatteryVoltage2 = 3.3m;
                device.LedStatus = "{\"seconds_remaining\":0}";
                device.SirenSecondsRemaining = 30;
                device.NightMode = 1;
                device.MotionSnoozeJson = "{\"until\":5}";
                device.SettingsMetadataJson = "{\"future_key\":1}";
                device.LightSettingsMetadataJson = "{\"schedule\":1}";
                device.PirSettingsSensitivity1 = 1;
                device.PirSettingsSensitivity2 = 2;
                device.PirSettingsSensitivity3 = 3;
                device.PirSettingsZoneEnable = 1;
                device.PirSettingsZoneMask = 15;
                device.NightModeOn = true;
                device.MotionDetectionEnabled = false;
                device.SettingsChimeType = 2;
                device.SettingsChimeEnabled = true;
                device.SettingsChimeDuration = 3;
                _ = await write.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = await _fixture.Factory.CreateDbContextAsync();
            Device stored = await read.Devices.SingleAsync(d => d.Id == _device.Id);
            Assert.Equal("doorbot", stored.RingType);
            Assert.Equal("abc", stored.RingRingId);
            Assert.Equal("55", stored.BatteryLife2);
            Assert.Equal(12.5m, stored.BatteryVoltage);
            Assert.Equal(3.3m, stored.BatteryVoltage2);
            Assert.Equal("{\"seconds_remaining\":0}", stored.LedStatus);
            Assert.Equal(30, stored.SirenSecondsRemaining);
            Assert.Equal(1, stored.NightMode);
            Assert.Equal("{\"until\":5}", stored.MotionSnoozeJson);
            Assert.Equal("{\"future_key\":1}", stored.SettingsMetadataJson);
            Assert.Equal("{\"schedule\":1}", stored.LightSettingsMetadataJson);
            Assert.Equal(15, stored.PirSettingsZoneMask);
            Assert.Equal(3, stored.PirSettingsSensitivity3);
            Assert.True(stored.NightModeOn);
            Assert.False(stored.MotionDetectionEnabled);
            Assert.Equal(2, stored.SettingsChimeType);
            Assert.True(stored.SettingsChimeEnabled);
            Assert.Equal(3, stored.SettingsChimeDuration);
        }

        [Fact]
        public async Task Device_SettingsScalarsOfEachType_PersistAndReadBack()
        {
            await using (VideoForensicsDbContext write = await _fixture.Factory.CreateDbContextAsync())
            {
                Device device = await write.Devices.SingleAsync(d => d.Id == _device.Id);
                device.EnableVod = true;                 // bool
                device.ExposureControl = 3;              // int
                device.VodStatus = "active";             // string
                device.DoNotDisturbSecondsLeft = 1800;   // int
                device.OwnerId = "42";                   // string
                device.OwnerEmail = "pat@example.com";   // string
                _ = await write.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = await _fixture.Factory.CreateDbContextAsync();
            Device stored = await read.Devices.SingleAsync(d => d.Id == _device.Id);
            Assert.True(stored.EnableVod);
            Assert.Equal(3, stored.ExposureControl);
            Assert.Equal("active", stored.VodStatus);
            Assert.Equal(1800, stored.DoNotDisturbSecondsLeft);
            Assert.Equal("42", stored.OwnerId);
            Assert.Equal("pat@example.com", stored.OwnerEmail);
            Assert.Null(stored.LightBrightness);
        }
    }
}
