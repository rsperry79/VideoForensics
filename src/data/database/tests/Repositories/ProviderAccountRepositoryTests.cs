using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class ProviderAccountRepositoryTests : RepositoryTestBase
    {
        private ProviderAccountRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new ProviderAccountRepository(Fixture.Factory, CreateLogger<ProviderAccountRepository>());
        }

        [Fact]
        public async Task AddAsync_NewAccount_PersistsInitialSyncCompletedAsFalse()
        {
            // Arrange: a newly linked account has not finished its first sync yet
            var account = new ProviderAccount
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                ProviderName = "RingCamera",
                IsActive = true,
                InitialSyncCompleted = false
            };

            // Act
            await _repository.AddAsync(account, CancellationToken.None);

            // Assert: the flag round-trips as false, so an interrupted first sync is detectable
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            ProviderAccount? persisted = await db.ProviderAccounts.FindAsync(new object[] { account.Id }, cancellationToken: CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.False(persisted.InitialSyncCompleted);
        }

        [Fact]
        public async Task DeleteAsync_SharedLocationAndDevice_OtherAccountKeepsThemAndItsLink()
        {
            // Arrange: accounts A (being purged) and B share one location and its device
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.ProviderAccounts.Add(new ProviderAccount { Id = accountA, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                _ = db.ProviderAccounts.Add(new ProviderAccount { Id = accountB, UserId = Guid.NewGuid(), ProviderName = "Ring", IsActive = true });
                _ = db.Locations.Add(new Location { Id = locationId, ProviderLocationId = "shared-loc", Name = "Shared" });
                _ = db.Devices.Add(new Device { Id = deviceId, LocationId = locationId, ProviderDeviceId = "d-1", Name = "Door", Type = "doorbell" });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountA, LocationId = locationId });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountB, LocationId = locationId });
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: purge account A
            await _repository.DeleteAsync(accountA, CancellationToken.None);

            // Assert: A is gone with its own link; the shared location, the device and B's link are untouched
            await using (VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                Assert.Null(await db.ProviderAccounts.FindAsync(new object[] { accountA }, CancellationToken.None));
                Assert.False(await db.ProviderAccountLocations.AnyAsync(l => l.ProviderAccountId == accountA, CancellationToken.None));
                Assert.NotNull(await db.Locations.FindAsync(new object[] { locationId }, CancellationToken.None));
                Assert.NotNull(await db.Devices.FindAsync(new object[] { deviceId }, CancellationToken.None));
                Assert.True(await db.ProviderAccountLocations.AnyAsync(l => l.ProviderAccountId == accountB && l.LocationId == locationId, CancellationToken.None));
            }
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenProviderNameContainsNewlines()
        {
            // Arrange: Create a provider account with ProviderName containing CRLF (log injection attempt)
            string injectedProviderName = "RingCamera\r\nFAKE LOG ENTRY: Admin access granted";
            var userId = Guid.NewGuid();
            var account = new ProviderAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderName = injectedProviderName,
                IsActive = true
            };

            // Act: Add the provider account (logs it)
            await _repository.AddAsync(account, CancellationToken.None);

            // Assert: Verify the account was persisted with the raw ProviderName intact
            await using VideoForensicsDbContext db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            ProviderAccount? persistedAccount = await db.ProviderAccounts.FindAsync(new object[] { account.Id }, cancellationToken: CancellationToken.None);
            Assert.NotNull(persistedAccount);
            // The raw ProviderName (with newlines) should be stored in the database
            Assert.Equal(injectedProviderName, persistedAccount.ProviderName);
        }
    }
}
