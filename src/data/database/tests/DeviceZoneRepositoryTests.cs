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
    /// <summary>Round-trips motion and package zones (with detection types and vertices) and advanced object settings through SQLite.</summary>
    public class DeviceZoneRepositoryTests : IAsyncLifetime
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

        private DeviceSettingsChildren ChildrenWithZone(string zoneId = "7")
        {
            var zone = new DeviceZone
            {
                Id = Guid.NewGuid(),
                DeviceId = _device.Id,
                Source = "motion",
                ZoneId = zoneId,
                Name = "Porch",
                State = 1,
                Style = "solid",
                DetectionTypes = [new DeviceZoneDetectionType { Id = Guid.NewGuid(), Position = 0, DetectionType = "human" }],
                Vertices =
                [
                    new DeviceZoneVertex { Id = Guid.NewGuid(), Position = 1, X = 0.1, Y = 0.2 },
                    new DeviceZoneVertex { Id = Guid.NewGuid(), Position = 2, X = 0.3, Y = null }
                ]
            };
            var setting = new DeviceAdvancedObjectSetting
            {
                Id = Guid.NewGuid(),
                DeviceId = _device.Id,
                SettingKey = "human_detection_confidence",
                Day = 0.8,
                Night = 0.6
            };
            return new DeviceSettingsChildren(
                MotionSnoozePresets: [],
                LiveViewPresets: [],
                AdvancedMotionZones: [],
                Zones: [zone],
                AdvancedObjectSettings: [setting]);
        }

        [Fact]
        public async Task UpsertAsync_ZonesWithDetectionTypesAndVertices_WritesRowsAndChildRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, ChildrenWithZone());

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceZone zone = await db.DeviceZones
                .Include(z => z.DetectionTypes)
                .Include(z => z.Vertices)
                .SingleAsync(z => z.DeviceId == _device.Id);
            Assert.Equal("motion", zone.Source);
            Assert.Equal("7", zone.ZoneId);
            Assert.Equal("solid", zone.Style);
            Assert.Equal(["human"], zone.DetectionTypes.Select(t => t.DetectionType));
            Assert.Equal(2, zone.Vertices.Count);
            Assert.Null(zone.Vertices.Single(v => v.Position == 2).Y);
            DeviceAdvancedObjectSetting setting = await db.DeviceAdvancedObjectSettings.SingleAsync(s => s.DeviceId == _device.Id);
            Assert.Equal("human_detection_confidence", setting.SettingKey);
            Assert.Equal(0.6, setting.Night);
        }

        [Fact]
        public async Task UpsertAsync_ZonesSecondCallWithEmptyList_RemovesStaleZoneAndChildRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, ChildrenWithZone());
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                new DeviceSettingsChildren([], [], [], Zones: [], AdvancedObjectSettings: []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.DeviceZones.CountAsync(z => z.DeviceId == _device.Id));
            Assert.Equal(0, await db.DeviceZoneDetectionTypes.CountAsync());
            Assert.Equal(0, await db.DeviceZoneVertices.CountAsync());
            Assert.Equal(0, await db.DeviceAdvancedObjectSettings.CountAsync(s => s.DeviceId == _device.Id));
        }

        [Fact]
        public async Task UpsertAsync_ZonesNull_LeavesExistingZoneRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, ChildrenWithZone());
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.DeviceZones.CountAsync(z => z.DeviceId == _device.Id));
            Assert.Equal(1, await db.DeviceAdvancedObjectSettings.CountAsync(s => s.DeviceId == _device.Id));
        }
    }
}
