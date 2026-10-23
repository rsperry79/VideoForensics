using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Typed settings rows (offline motion, detection types, thresholds) are replaced per device; null leaves them untouched.</summary>
    public class DeviceTypedSettingsRepositoryTests : IAsyncLifetime
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

        [Fact]
        public async Task UpsertAsync_TypedSettings_WritesOfflineDetectionTypesAndThresholds()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(
                offline: [Offline(5000)],
                types: [Type("human"), Type("loitering")],
                thresholds: [Threshold("loitering", 10.0)]));

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Equal(5000, (await db.DeviceOfflineMotionSettings.SingleAsync()).MaxUploadKb);
            Assert.Equal(2, await db.DeviceDetectionTypes.CountAsync(t => t.DeviceId == _device.Id));
            Assert.Equal(10.0, (await db.DeviceDetectionThresholds.SingleAsync()).Threshold);
        }

        [Fact]
        public async Task UpsertAsync_TypedSettingsSecondWrite_ReplacesDetectionTypes()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(types: [Type("human"), Type("loitering")]));

            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(types: [Type("package_delivery")]));

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            DeviceDetectionType only = await db.DeviceDetectionTypes.SingleAsync(t => t.DeviceId == _device.Id);
            Assert.Equal("package_delivery", only.DetectionType);
        }

        [Fact]
        public async Task UpsertAsync_TypedSettingsNullLists_LeavesStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(offline: [Offline(5000)], types: [Type("human")]));

            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings());

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = Assert.Single(await db.DeviceOfflineMotionSettings.ToListAsync());
            _ = Assert.Single(await db.DeviceDetectionTypes.ToListAsync());
        }

        [Fact]
        public async Task UpsertAsync_TypedSettingsEmptyLists_ClearsStoredRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(offline: [Offline(5000)], types: [Type("human")]));

            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, Settings(offline: [], types: []));

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Assert.Empty(await db.DeviceOfflineMotionSettings.ToListAsync());
            Assert.Empty(await db.DeviceDetectionTypes.ToListAsync());
        }

        private static DeviceSettingsChildren Settings(
            IReadOnlyList<DeviceOfflineMotionSettings>? offline = null,
            IReadOnlyList<DeviceDetectionType>? types = null,
            IReadOnlyList<DeviceDetectionThreshold>? thresholds = null)
        {
            return new(MotionSnoozePresets: [], LiveViewPresets: [], AdvancedMotionZones: [], OfflineMotion: offline, DetectionTypes: types, DetectionThresholds: thresholds);
        }

        private static DeviceOfflineMotionSettings Offline(int maxUploadKb)
        {
            return new() { Id = Guid.NewGuid(), MaxUploadKb = maxUploadKb, Enabled = false };
        }

        private static DeviceDetectionType Type(string name)
        {
            return new() { Id = Guid.NewGuid(), DetectionType = name, Enabled = true, Mode = "cloud" };
        }

        private static DeviceDetectionThreshold Threshold(string name, double value)
        {
            return new() { Id = Guid.NewGuid(), DetectionType = name, Threshold = value };
        }
    }
}
