using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceRepository_AddAndGet_RoundTrips()
        {
            var locationId = Guid.NewGuid();
            Device device = TestDataBuilder.BuildDevice(locationId, "dev_001", "Front Camera");

            await _repository.AddAsync(device, CancellationToken.None);
            Device? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(device.Id, retrieved.Id);
            Assert.Equal(locationId, retrieved.LocationId);
            Assert.Equal("dev_001", retrieved.ProviderDeviceId);
            Assert.Equal("Front Camera", retrieved.Name);
        }

        [Fact]
        public async Task DeviceRepository_GetByProviderDeviceId_FindsDevice()
        {
            var locationId = Guid.NewGuid();
            Device device = TestDataBuilder.BuildDevice(locationId, "dev_002");

            await _repository.AddAsync(device, CancellationToken.None);
            Device? retrieved = await _repository.GetByProviderDeviceIdAsync(locationId, "dev_002", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(device.Id, retrieved.Id);
        }

        [Fact]
        public async Task DeviceRepository_GetByLocationId_ReturnsAllForLocation()
        {
            var locationId = Guid.NewGuid();
            var otherLocationId = Guid.NewGuid();

            Device dev1 = TestDataBuilder.BuildDevice(locationId);
            Device dev2 = TestDataBuilder.BuildDevice(locationId);
            Device dev3 = TestDataBuilder.BuildDevice(otherLocationId);

            await _repository.AddAsync(dev1, CancellationToken.None);
            await _repository.AddAsync(dev2, CancellationToken.None);
            await _repository.AddAsync(dev3, CancellationToken.None);

            IReadOnlyList<Device> list = await _repository.GetByLocationIdAsync(locationId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task DeviceRepository_UpdateAsync_ModifiesData()
        {
            Device device = TestDataBuilder.BuildDevice();
            await _repository.AddAsync(device, CancellationToken.None);

            device.Name = "Updated Camera";
            await _repository.UpdateAsync(device, CancellationToken.None);

            Device? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Camera", retrieved.Name);
        }

        [Fact]
        public async Task DeviceRepository_UpdateLastSuccessfulPullAsync_UpdatesTimestamp()
        {
            Device device = TestDataBuilder.BuildDevice();
            await _repository.AddAsync(device, CancellationToken.None);

            DateTime pullTime = DateTime.UtcNow;
            await _repository.UpdateLastSuccessfulPullAsync(device.Id, pullTime, CancellationToken.None);

            Device? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(pullTime, retrieved.LastSuccessfulPullAtUtc);
        }

        [Fact]
        public async Task DeviceRepository_DeleteAsync_RemovesDevice()
        {
            Device device = TestDataBuilder.BuildDevice();
            await _repository.AddAsync(device, CancellationToken.None);

            await _repository.DeleteAsync(device.Id, CancellationToken.None);

            Device? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task DeviceRepository_ListAsync_ReturnsAll()
        {
            Device dev1 = TestDataBuilder.BuildDevice();
            Device dev2 = TestDataBuilder.BuildDevice();

            await _repository.AddAsync(dev1, CancellationToken.None);
            await _repository.AddAsync(dev2, CancellationToken.None);

            IReadOnlyList<Device> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task DeviceRepository_UniqueConstraint_DuplicateLocationDeviceComboThrows()
        {
            var locationId = Guid.NewGuid();
            Device dev1 = TestDataBuilder.BuildDevice(locationId, "dup_dev");
            Device dev2 = TestDataBuilder.BuildDevice(locationId, "dup_dev");

            await _repository.AddAsync(dev1, CancellationToken.None);

            _ = await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(dev2, CancellationToken.None));
        }

        [Fact]
        public async Task DeviceRepository_AddAndGet_RoundTripsDeviceDetailFields()
        {
            var locationId = Guid.NewGuid();
            Device device = TestDataBuilder.BuildDevice(locationId, "dev_detail", "Front Camera");
            device.RingDeviceId = "aabbcc";
            device.RingKind = "doorbell_v4";
            device.FirmwareVersion = "2.0.1";
            device.Latitude = 41.5;
            device.Longitude = -87.6;
            device.Address = "1 Main St";
            device.Subscribed = true;
            device.SubscribedMotions = false;
            device.BatteryLife = "87";
            device.ExternalConnection = true;
            device.Owned = true;
            device.Stolen = false;

            await _repository.AddAsync(device, CancellationToken.None);
            Device? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("aabbcc", retrieved.RingDeviceId);
            Assert.Equal("doorbell_v4", retrieved.RingKind);
            Assert.Equal("2.0.1", retrieved.FirmwareVersion);
            Assert.Equal(41.5, retrieved.Latitude);
            Assert.Equal(-87.6, retrieved.Longitude);
            Assert.Equal("1 Main St", retrieved.Address);
            Assert.True(retrieved.Subscribed);
            Assert.False(retrieved.SubscribedMotions);
            Assert.Equal("87", retrieved.BatteryLife);
            Assert.True(retrieved.ExternalConnection);
            Assert.True(retrieved.Owned);
            Assert.False(retrieved.Stolen);
        }
    }
}
