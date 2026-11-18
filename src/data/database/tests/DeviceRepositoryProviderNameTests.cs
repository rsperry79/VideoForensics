using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Every device read fills Device.ProviderName from the provider account that is linked to the device's location.</summary>
    public class DeviceRepositoryProviderNameTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceRepository(new UnitOfWork(_fixture.Factory, new Repositories.TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>()), loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceRepository_ListAsync_LinkedLocation_SetsProviderNameFromAccount()
        {
            Guid accountId = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtLinkedLocationAsync(accountId);

            IReadOnlyList<Device> devices = await _repository.ListAsync(CancellationToken.None);

            Device listed = Assert.Single(devices, d => d.Id == device.Id);
            Assert.Equal("Ring", listed.ProviderName);
        }

        [Fact]
        public async Task DeviceRepository_ListAsync_UnlinkedLocation_LeavesProviderNameNull()
        {
            Device device = await SeedDeviceWithoutLinkAsync();

            IReadOnlyList<Device> devices = await _repository.ListAsync(CancellationToken.None);

            Device listed = Assert.Single(devices, d => d.Id == device.Id);
            Assert.Null(listed.ProviderName);
        }

        [Fact]
        public async Task DeviceRepository_GetByLocationIdAsync_LinkedLocation_SetsProviderNameFromAccount()
        {
            Guid accountId = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtLinkedLocationAsync(accountId);

            IReadOnlyList<Device> devices = await _repository.GetByLocationIdAsync(device.LocationId, CancellationToken.None);

            Assert.Equal("Ring", Assert.Single(devices).ProviderName);
        }

        [Fact]
        public async Task DeviceRepository_GetAsync_LinkedLocation_SetsProviderNameFromAccount()
        {
            Guid accountId = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtLinkedLocationAsync(accountId);

            Device? found = await _repository.GetAsync(device.Id, CancellationToken.None);

            Assert.Equal("Ring", found?.ProviderName);
        }

        [Fact]
        public async Task DeviceRepository_GetByProviderDeviceIdAsync_LinkedLocation_SetsProviderNameFromAccount()
        {
            Guid accountId = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtLinkedLocationAsync(accountId);

            Device? found = await _repository.GetByProviderDeviceIdAsync(device.LocationId, device.ProviderDeviceId, CancellationToken.None);

            Assert.Equal("Ring", found?.ProviderName);
        }

        [Fact]
        public async Task DeviceRepository_GetAsync_UnlinkedLocation_LeavesProviderNameNull()
        {
            Device device = await SeedDeviceWithoutLinkAsync();

            Device? found = await _repository.GetAsync(device.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Null(found.ProviderName);
        }

        private async Task<Guid> SeedAccountAsync(string providerName)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            User user = TestDataBuilder.BuildUser();
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(user.Id, providerName);
            _ = db.Users.Add(user);
            _ = db.ProviderAccounts.Add(account);
            _ = await db.SaveChangesAsync();
            return account.Id;
        }

        private async Task<Device> SeedDeviceAtLinkedLocationAsync(Guid providerAccountId)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            _ = db.Locations.Add(location);
            _ = db.Devices.Add(device);
            _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = providerAccountId, LocationId = location.Id });
            _ = await db.SaveChangesAsync();
            return device;
        }

        private async Task<Device> SeedDeviceWithoutLinkAsync()
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            _ = db.Locations.Add(location);
            _ = db.Devices.Add(device);
            _ = await db.SaveChangesAsync();
            return device;
        }
    }
}
