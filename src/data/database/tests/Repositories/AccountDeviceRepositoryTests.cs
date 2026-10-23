using VideoForensics.Data.Common.Entities;
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
            Guid accountA = Guid.NewGuid();
            Guid accountB = Guid.NewGuid();
            Guid locationOne = Guid.NewGuid();
            Guid locationTwo = Guid.NewGuid();
            Guid deviceOne = Guid.NewGuid();
            Guid deviceTwo = Guid.NewGuid();

            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.ProviderAccounts.Add(new ProviderAccount { Id = accountA, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                db.ProviderAccounts.Add(new ProviderAccount { Id = accountB, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                db.Locations.Add(new Location { Id = locationOne, ProviderLocationId = "loc-1", Name = "Home" });
                db.Locations.Add(new Location { Id = locationTwo, ProviderLocationId = "loc-2", Name = "Office" });
                db.Devices.Add(new Device { Id = deviceOne, LocationId = locationOne, ProviderDeviceId = "1", Name = "Door", Type = "doorbell" });
                db.Devices.Add(new Device { Id = deviceTwo, LocationId = locationTwo, ProviderDeviceId = "2", Name = "Yard", Type = "camera" });
                db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountA, LocationId = locationOne });
                db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountB, LocationId = locationTwo });
                await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act
            IReadOnlyList<Device> devices = await _repository.ListByProviderAccountAsync(accountA, CancellationToken.None);

            // Assert
            Assert.Single(devices);
            Assert.Equal(deviceOne, devices[0].Id);
        }
    }
}
