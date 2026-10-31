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
    /// <summary>Round-trips the endpoint group rows (RingDevice group tables, misc, and the snooze and threshold child rows) through SQLite: replace-on-sync and null-leaves-untouched.</summary>
    public class RingSettingGroupRowsRepositoryTests : IAsyncLifetime
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

        private static DeviceSettingsChildren Children(RingDeviceSettingGroupRows? groups)
        {
            return new DeviceSettingsChildren([], [], [], RingSettingGroups: groups);
        }

        private static RingDeviceSettingGroupRows Groups(int exposure, params int[] snooze)
        {
            return new RingDeviceSettingGroupRows(
                AdvancedMotion: null,
                Backend: null,
                ClientDevice: null,
                Cv: null,
                General: null,
                Misc: new RingDeviceMiscSettings { Type = "fake-type" },
                Motion: new RingDeviceMotionSettings { EnableIr = 1 },
                Snapshot: null,
                Stream: null,
                Video: new RingDeviceVideoSettings { ExposureControl = exposure, IgnoreZonesZone1Vertex1Y = 0 },
                Vod: null,
                MotionSnoozeProfiles: snooze.Select((v, i) => new RingDeviceMotionSnoozeProfile { Position = i, Value = v }).ToList(),
                CvThresholdVersions: [],
                CvThresholdValues: []);
        }

        [Fact]
        public async Task UpsertAsync_GroupRows_WritesOneRowPerDeviceAndOneChildRowPerElement()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Groups(2, 5, 15)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceVideoSettings video = await db.RingDeviceVideoSettings.SingleAsync(v => v.DeviceId == _device.Id);
            Assert.Equal(2, video.ExposureControl);
            Assert.Equal(0, video.IgnoreZonesZone1Vertex1Y);
            Assert.NotEqual(default, video.UpdatedAtUtc);
            Assert.Null(video.AspectRatio);
            Assert.Equal("fake-type", (await db.RingDeviceMiscSettings.SingleAsync(m => m.DeviceId == _device.Id)).Type);
            Assert.Equal(1, (await db.RingDeviceMotionSettings.SingleAsync(m => m.DeviceId == _device.Id)).EnableIr);
            Assert.Equal(2, await db.RingDeviceMotionSnoozeProfiles.CountAsync(p => p.DeviceId == _device.Id));
        }

        [Fact]
        public async Task UpsertAsync_SecondCall_ReplacesRowsWithoutDuplicates()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Groups(2, 5, 15)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Groups(4, 30)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.RingDeviceVideoSettings.CountAsync());
            Assert.Equal(4, (await db.RingDeviceVideoSettings.SingleAsync()).ExposureControl);
            Assert.Equal(1, await db.RingDeviceMotionSnoozeProfiles.CountAsync());
            Assert.Equal(30, (await db.RingDeviceMotionSnoozeProfiles.SingleAsync()).Value);
        }

        [Fact]
        public async Task UpsertAsync_NullGroupRows_LeaveStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Groups(2, 5)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(null));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(2, (await db.RingDeviceVideoSettings.SingleAsync()).ExposureControl);
            Assert.Equal(1, await db.RingDeviceMotionSnoozeProfiles.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_EmptyGroupRows_WriteRowsOfNullsAndClearChildRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(Groups(2, 5, 15)));
            var empty = new RingDeviceSettingGroupRows(null, null, null, null, null, null, null, null, null, new RingDeviceVideoSettings(), null, [], [], []);
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Children(empty));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceVideoSettings video = await db.RingDeviceVideoSettings.SingleAsync();
            Assert.Null(video.ExposureControl);
            Assert.Equal(0, await db.RingDeviceMotionSnoozeProfiles.CountAsync());
        }
    }
}
