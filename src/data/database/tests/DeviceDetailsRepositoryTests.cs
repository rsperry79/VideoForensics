using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceDetailsRepositoryTests : IAsyncLifetime
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

        private static DeviceCapabilities Caps(Guid deviceId, bool? hasMotion = true, string? firmware = "1.2.3") => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            HasAudio = true,
            HasMotionDetection = hasMotion,
            HasNightVision = false,
            FirmwareVersion = firmware,
            SyncStatus = SyncStatus.Synced
        };

        [Fact]
        public async Task UpsertAsync_NewDevice_InsertsCapabilitiesFeaturesAndAlertsAndLinksFeaturesId()
        {
            var features = new DeviceFeatures { Id = Guid.NewGuid(), DeviceId = _device.Id, MotionsEnabled = true, NightVisionEnabled = false };
            var alerts = new List<DeviceAlerts> { new() { Id = Guid.NewGuid(), DeviceId = _device.Id, AlertType = "connection", IsEnabled = null } };

            await _repository.UpsertAsync(_device.Id, null, Caps(_device.Id), features, alerts, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceCapabilities storedCaps = await db.DeviceCapabilities.SingleAsync(c => c.DeviceId == _device.Id);
            Assert.True(storedCaps.HasAudio);
            Assert.True(storedCaps.HasMotionDetection);
            Assert.Equal("1.2.3", storedCaps.FirmwareVersion);

            DeviceFeatures storedFeatures = await db.DeviceFeatures.SingleAsync(f => f.DeviceId == _device.Id);
            Assert.True(storedFeatures.MotionsEnabled);

            DeviceAlerts storedAlert = await db.DeviceAlerts.SingleAsync(a => a.DeviceId == _device.Id);
            Assert.Equal("connection", storedAlert.AlertType);
            Assert.Null(storedAlert.IsEnabled);

            Device device = await db.Devices.SingleAsync(d => d.Id == _device.Id);
            Assert.Equal(storedFeatures.Id, device.DeviceFeaturesId);
        }

        [Fact]
        public async Task UpsertAsync_CalledTwice_UpdatesExistingRowsInsteadOfDuplicating()
        {
            await _repository.UpsertAsync(_device.Id, null, Caps(_device.Id, hasMotion: true, firmware: "1.0"), null, null, CancellationToken.None);
            await _repository.UpsertAsync(_device.Id, null, Caps(_device.Id, hasMotion: false, firmware: "2.0"), null, null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceCapabilities stored = await db.DeviceCapabilities.SingleAsync(c => c.DeviceId == _device.Id);
            Assert.False(stored.HasMotionDetection);
            Assert.Equal("2.0", stored.FirmwareVersion);
        }

        [Fact]
        public async Task UpsertAsync_SameAlertType_UpdatesRowInsteadOfDuplicating()
        {
            var first = new List<DeviceAlerts> { new() { Id = Guid.NewGuid(), DeviceId = _device.Id, AlertType = "connection", IsEnabled = null } };
            var second = new List<DeviceAlerts> { new() { Id = Guid.NewGuid(), DeviceId = _device.Id, AlertType = "connection", IsEnabled = true } };

            await _repository.UpsertAsync(_device.Id, null, null, null, first, CancellationToken.None);
            await _repository.UpsertAsync(_device.Id, null, null, null, second, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceAlerts stored = await db.DeviceAlerts.SingleAsync(a => a.DeviceId == _device.Id);
            Assert.True(stored.IsEnabled);
        }

        [Fact]
        public async Task UpsertAsync_HealthId_LinksDeviceHealthId()
        {
            Guid healthId = Guid.NewGuid();

            await _repository.UpsertAsync(_device.Id, healthId, null, null, null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Device device = await db.Devices.SingleAsync(d => d.Id == _device.Id);
            Assert.Equal(healthId, device.DeviceHealthId);
            Assert.Empty(await db.DeviceCapabilities.Where(c => c.DeviceId == _device.Id).ToListAsync());
        }

        [Fact]
        public async Task UpsertAsync_UnknownDevice_WritesNothing()
        {
            Guid unknown = Guid.NewGuid();

            await _repository.UpsertAsync(unknown, Guid.NewGuid(), Caps(unknown), null, null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Empty(await db.DeviceCapabilities.Where(c => c.DeviceId == unknown).ToListAsync());
        }
    }
}
