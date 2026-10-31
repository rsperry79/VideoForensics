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
    /// <summary>Round-trips the Ring ignore zones, motion-zone values and detection-type names through SQLite: replace-on-sync, empty lists clear, null leaves untouched.</summary>
    public class RingDeviceZoneRowsRepositoryTests : IAsyncLifetime
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

        private static RingDeviceIgnoreZone IgnoreZone(int number, string name, params (int Position, double X, double Y)[] vertices)
        {
            return new RingDeviceIgnoreZone
            {
                ZoneNumber = number,
                Name = name,
                State = 1,
                Vertices = vertices.Select(v => new RingDeviceIgnoreZoneVertex { Position = v.Position, X = v.X, Y = v.Y }).ToList()
            };
        }

        private async Task SaveAsync(List<RingDeviceIgnoreZone>? zones, List<long?>? motion, List<string>? types)
        {
            var children = new DeviceSettingsChildren(
                [], [], [],
                RingIgnoreZones: zones,
                RingMotionZones: motion?.Select((v, i) => new RingDeviceMotionZone { Position = i, Value = v }).ToList(),
                RingAdvancedMotionDetectionTypes: types?.Select((t, i) => new RingDeviceAdvancedMotionDetectionType { Position = i, DetectionType = t }).ToList());
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, children);
        }

        [Fact]
        public async Task UpsertAsync_IgnoreZones_WriteZonesAndVerticesWithPositions()
        {
            await SaveAsync([IgnoreZone(2, "Fake Zone", (1, 0.1, 0.2), (2, 0.3, 0.5))], null, null);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceIgnoreZone zone = await db.RingDeviceIgnoreZones.SingleAsync(z => z.DeviceId == _device.Id);
            Assert.Equal(2, zone.ZoneNumber);
            Assert.Equal("Fake Zone", zone.Name);
            Assert.Equal(2, await db.RingDeviceIgnoreZoneVertices.CountAsync(v => v.RingDeviceIgnoreZoneId == zone.Id));
            Assert.Equal(0.5, (await db.RingDeviceIgnoreZoneVertices.SingleAsync(v => v.RingDeviceIgnoreZoneId == zone.Id && v.Position == 2)).Y);
        }

        [Fact]
        public async Task UpsertAsync_SecondCall_ReplacesIgnoreZonesAndTheirVertices()
        {
            await SaveAsync([IgnoreZone(1, "Old", (1, 0, 0), (2, 1, 1)), IgnoreZone(2, "Also old", (1, 0, 0))], null, null);
            await SaveAsync([IgnoreZone(1, "New", (1, 0.9, 0.9))], null, null);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDeviceIgnoreZone zone = await db.RingDeviceIgnoreZones.SingleAsync();
            Assert.Equal("New", zone.Name);
            Assert.Equal(1, await db.RingDeviceIgnoreZoneVertices.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_NullZoneLists_LeaveStoredRowsUntouched()
        {
            await SaveAsync([IgnoreZone(1, "Kept", (1, 0, 0))], [1L, 0L], ["human"]);
            await SaveAsync(null, null, null);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal("Kept", (await db.RingDeviceIgnoreZones.SingleAsync()).Name);
            Assert.Equal(2, await db.RingDeviceMotionZones.CountAsync(m => m.DeviceId == _device.Id));
            Assert.Equal("human", (await db.RingDeviceAdvancedMotionDetectionTypes.SingleAsync()).DetectionType);
        }

        [Fact]
        public async Task UpsertAsync_EmptyLists_ClearStoredMotionZonesAndDetectionTypes()
        {
            await SaveAsync(null, [1L, 0L, 1L], ["human", "vehicle"]);
            await SaveAsync([], [], []);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.RingDeviceMotionZones.CountAsync(m => m.DeviceId == _device.Id));
            Assert.Equal(0, await db.RingDeviceAdvancedMotionDetectionTypes.CountAsync(t => t.DeviceId == _device.Id));
            Assert.Equal(0, await db.RingDeviceIgnoreZones.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_MotionZonesAndDetectionTypes_KeepRingOrder()
        {
            await SaveAsync(null, [0L, 1L, 0L], ["human", "vehicle"]);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(new long?[] { 0, 1, 0 }, await db.RingDeviceMotionZones.Where(m => m.DeviceId == _device.Id).OrderBy(m => m.Position).Select(m => m.Value).ToArrayAsync());
            Assert.Equal(new[] { "human", "vehicle" }, await db.RingDeviceAdvancedMotionDetectionTypes.Where(t => t.DeviceId == _device.Id).OrderBy(t => t.Position).Select(t => t.DetectionType).ToArrayAsync());
        }
    }
}
