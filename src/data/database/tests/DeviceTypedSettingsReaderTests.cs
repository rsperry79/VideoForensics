using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The reader returns only the requested device's typed rows, ordered by detection type.</summary>
    public class DeviceTypedSettingsReaderTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceTypedSettingsReader _reader = null!;
        private Device _device = null!;
        private Device _other = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _reader = new DeviceTypedSettingsReader(_fixture.Factory);

            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var locationRepository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
            var deviceRepository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
            Location location = TestDataBuilder.BuildLocation();
            await locationRepository.AddAsync(location, CancellationToken.None);
            _device = TestDataBuilder.BuildDevice(locationId: location.Id);
            _other = TestDataBuilder.BuildDevice(locationId: location.Id);
            await deviceRepository.AddAsync(_device, CancellationToken.None);
            await deviceRepository.AddAsync(_other, CancellationToken.None);

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.DeviceOfflineMotionSettings.Add(new DeviceOfflineMotionSettings { Id = Guid.NewGuid(), DeviceId = _device.Id, MaxUploadKb = 5000 });
            _ = db.DeviceDetectionTypes.Add(new DeviceDetectionType { Id = Guid.NewGuid(), DeviceId = _device.Id, DetectionType = "package_delivery" });
            _ = db.DeviceDetectionTypes.Add(new DeviceDetectionType { Id = Guid.NewGuid(), DeviceId = _device.Id, DetectionType = "human" });
            _ = db.DeviceDetectionThresholds.Add(new DeviceDetectionThreshold { Id = Guid.NewGuid(), DeviceId = _device.Id, DetectionType = "loitering", Threshold = 10.0 });
            _ = db.DeviceDetectionTypes.Add(new DeviceDetectionType { Id = Guid.NewGuid(), DeviceId = _other.Id, DetectionType = "vehicle" });
            _ = await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceTypedSettingsReader_GetAsync_ReturnsOnlyThatDevicesRowsOrderedByType()
        {
            DeviceTypedSettings settings = await _reader.GetAsync(_device.Id, CancellationToken.None);

            Assert.Equal(5000, settings.OfflineMotion!.MaxUploadKb);
            Assert.Equal(new[] { "human", "package_delivery" }, settings.DetectionTypes.Select(t => t.DetectionType));
            Assert.Equal(10.0, Assert.Single(settings.Thresholds).Threshold);
        }

        [Fact]
        public async Task DeviceTypedSettingsReader_GetAsync_DeviceWithNoRows_ReturnsEmptyGroups()
        {
            Device empty = TestDataBuilder.BuildDevice(locationId: _device.LocationId);
            await new DeviceRepository(_fixture.Factory, Microsoft.Extensions.Logging.LoggerFactory.Create(b => { }).CreateLogger<DeviceRepository>()).AddAsync(empty, CancellationToken.None);

            DeviceTypedSettings settings = await _reader.GetAsync(empty.Id, CancellationToken.None);

            Assert.Null(settings.OfflineMotion);
            Assert.Empty(settings.DetectionTypes);
            Assert.Empty(settings.Thresholds);
        }
    }
}
