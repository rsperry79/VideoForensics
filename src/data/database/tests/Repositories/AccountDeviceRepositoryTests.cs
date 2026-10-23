using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class AccountDeviceRepositoryTests : RepositoryTestBase
    {
        private AccountDeviceRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new AccountDeviceRepository(Fixture.Factory, CreateLogger<AccountDeviceRepository>());
        }

        [Fact]
        public async Task ListByProviderAccountAsync_ReturnsOnlyDevicesAtLinkedLocations()
        {
            // Arrange: account A is linked to location 1 only; location 2 belongs to account B
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            var locationOne = Guid.NewGuid();
            var locationTwo = Guid.NewGuid();
            var deviceOne = Guid.NewGuid();
            var deviceTwo = Guid.NewGuid();

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.ProviderAccounts.Add(new ProviderAccount { Id = accountA, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                _ = db.ProviderAccounts.Add(new ProviderAccount { Id = accountB, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                _ = db.Locations.Add(new Location { Id = locationOne, ProviderLocationId = "loc-1", Name = "Home" });
                _ = db.Locations.Add(new Location { Id = locationTwo, ProviderLocationId = "loc-2", Name = "Office" });
                _ = db.Devices.Add(new Device { Id = deviceOne, LocationId = locationOne, ProviderDeviceId = "1", Name = "Door", Type = "doorbell" });
                _ = db.Devices.Add(new Device { Id = deviceTwo, LocationId = locationTwo, ProviderDeviceId = "2", Name = "Yard", Type = "camera" });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountA, LocationId = locationOne });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountB, LocationId = locationTwo });
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<Device> devices = await _repository.ListByProviderAccountAsync(accountA, CancellationToken.None);

            // Assert
            _ = Assert.Single(devices);
            Assert.Equal(deviceOne, devices[0].Id);
        }
    }
}
