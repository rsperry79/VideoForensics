using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class ProviderAccountRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ProviderAccountRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ProviderAccountRepository(_fixture.Factory, loggerFactory.CreateLogger<ProviderAccountRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ProviderAccountRepository_AddAndGet_RoundTrips()
        {
            var userId = Guid.NewGuid();
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId, "Ring");

            await _repository.AddAsync(account, CancellationToken.None);
            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(account.Id, retrieved.Id);
            Assert.Equal(userId, retrieved.UserId);
            Assert.Equal("Ring", retrieved.ProviderName);
            Assert.True(retrieved.IsActive);
        }

        [Fact]
        public async Task ProviderAccountRepository_GetByUserAndProvider_FindsAccount()
        {
            var userId = Guid.NewGuid();
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId, "Wyze");

            await _repository.AddAsync(account, CancellationToken.None);
            ProviderAccount? retrieved = await _repository.GetByUserAndProviderAsync(userId, "Wyze", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(account.Id, retrieved.Id);
        }

        [Fact]
        public async Task ProviderAccountRepository_GetByUserId_ReturnsAllForUser()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            ProviderAccount account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
            ProviderAccount account2 = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
            ProviderAccount account3 = TestDataBuilder.BuildProviderAccount(otherUserId, "Ring");

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);
            await _repository.AddAsync(account3, CancellationToken.None);

            IReadOnlyList<ProviderAccount> list = await _repository.GetByUserIdAsync(userId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ProviderAccountRepository_UpdateAsync_ModifiesData()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            account.IsActive = false;
            await _repository.UpdateAsync(account, CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.IsActive);
        }

        [Fact]
        public async Task ProviderAccountRepository_DeleteAsync_RemovesAccount()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            await _repository.DeleteAsync(account.Id, CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task ProviderAccountRepository_DeleteAsync_RemovesSyncSchedule()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None); // AddAsync also creates the default SyncSchedule

            await using (VideoForensicsDbContext before = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                Assert.True(await before.SyncSchedules.AnyAsync(s => s.ProviderAccountId == account.Id));
            }

            await _repository.DeleteAsync(account.Id, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Assert.False(await db.SyncSchedules.AnyAsync(s => s.ProviderAccountId == account.Id));
        }

        [Fact]
        public async Task ProviderAccountRepository_ListAsync_ReturnsAll()
        {
            ProviderAccount account1 = TestDataBuilder.BuildProviderAccount();
            ProviderAccount account2 = TestDataBuilder.BuildProviderAccount();

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);

            IReadOnlyList<ProviderAccount> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ProviderAccountRepository_ListActiveAsync_ReturnsOnlyActive()
        {
            ProviderAccount account1 = TestDataBuilder.BuildProviderAccount();
            account1.IsActive = true;

            ProviderAccount account2 = TestDataBuilder.BuildProviderAccount();
            account2.IsActive = false;

            await _repository.AddAsync(account1, CancellationToken.None);
            await _repository.AddAsync(account2, CancellationToken.None);

            IReadOnlyList<ProviderAccount> list = await _repository.ListActiveAsync(CancellationToken.None);
            _ = Assert.Single(list);
            Assert.Equal(account1.Id, list[0].Id);
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_SetsRemovedUtc()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);
            DateTime removedUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            await _repository.RetireAsync(account.Id, removedUtc, CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(removedUtc, retrieved.RemovedUtc);
            Assert.False(retrieved.IsActive);
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_RemovesSyncSchedule()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None); // AddAsync also creates the default SyncSchedule

            await _repository.RetireAsync(account.Id, DateTime.UtcNow, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Assert.False(await db.SyncSchedules.AnyAsync(s => s.ProviderAccountId == account.Id));
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_RemovesCredentials()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);
            ProviderAccount other = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(other, CancellationToken.None);
            await using (VideoForensicsDbContext seed = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = seed.Credentials.Add(new Credential { Id = Guid.NewGuid(), ProviderAccountId = account.Id, CredentialType = "Password", EncryptedValue = "x", EncryptionProvider = "test", CreatedUtc = DateTime.UtcNow });
                _ = seed.Credentials.Add(new Credential { Id = Guid.NewGuid(), ProviderAccountId = other.Id, CredentialType = "Password", EncryptedValue = "y", EncryptionProvider = "test", CreatedUtc = DateTime.UtcNow });
                _ = await seed.SaveChangesAsync();
            }

            await _repository.RetireAsync(account.Id, DateTime.UtcNow, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Assert.False(await db.Credentials.AnyAsync(c => c.ProviderAccountId == account.Id));
            Assert.True(await db.Credentials.AnyAsync(c => c.ProviderAccountId == other.Id));
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_KeepsAccountRow()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            await _repository.RetireAsync(account.Id, DateTime.UtcNow, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Assert.True(await db.ProviderAccounts.AnyAsync(a => a.Id == account.Id));
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_AlreadyRemoved_KeepsOriginalRemovedUtc()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);
            DateTime first = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            await _repository.RetireAsync(account.Id, first, CancellationToken.None);
            await _repository.RetireAsync(account.Id, first.AddDays(1), CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.Equal(first, retrieved!.RemovedUtc);
        }

        [Fact]
        public async Task ProviderAccountRepository_RetireAsync_MissingAccount_DoesNotThrow()
        {
            await _repository.RetireAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None);
        }

        [Fact]
        public async Task ProviderAccountRepository_ListActiveAsync_ExcludesRemoved()
        {
            ProviderAccount kept = TestDataBuilder.BuildProviderAccount();
            ProviderAccount removed = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(kept, CancellationToken.None);
            await _repository.AddAsync(removed, CancellationToken.None);
            await _repository.RetireAsync(removed.Id, DateTime.UtcNow, CancellationToken.None);

            IReadOnlyList<ProviderAccount> list = await _repository.ListActiveAsync(CancellationToken.None);

            _ = Assert.Single(list);
            Assert.Equal(kept.Id, list[0].Id);
            Assert.Equal(2, (await _repository.ListAsync(CancellationToken.None)).Count);
        }

        [Fact]
        public async Task ProviderAccountRepository_ListActiveAsync_RemovedButStillFlaggedActive_IsExcluded()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            account.RemovedUtc = DateTime.UtcNow;
            account.IsActive = true;
            await _repository.AddAsync(account, CancellationToken.None);

            Assert.Empty(await _repository.ListActiveAsync(CancellationToken.None));
        }

        [Fact]
        public async Task ProviderAccountRepository_ListRemovedAsync_ReturnsOnlyRemoved()
        {
            ProviderAccount active = TestDataBuilder.BuildProviderAccount();
            ProviderAccount older = TestDataBuilder.BuildProviderAccount();
            ProviderAccount newer = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(active, CancellationToken.None);
            await _repository.AddAsync(older, CancellationToken.None);
            await _repository.AddAsync(newer, CancellationToken.None);
            await _repository.RetireAsync(older.Id, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
            await _repository.RetireAsync(newer.Id, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

            IReadOnlyList<ProviderAccount> list = await _repository.ListRemovedAsync(CancellationToken.None);

            Assert.Equal([newer.Id, older.Id], list.Select(a => a.Id).ToArray());
        }

        [Fact]
        public async Task ProviderAccountRepository_UniqueConstraint_DuplicateUserProviderComboThrows()
        {
            var userId = Guid.NewGuid();
            ProviderAccount account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
            ProviderAccount account2 = TestDataBuilder.BuildProviderAccount(userId, "Ring");

            await _repository.AddAsync(account1, CancellationToken.None);

            _ = await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(account2, CancellationToken.None));
        }

        [Fact]
        public async Task ProviderAccountRepository_RecordErrorAsync_SetsErrorState()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            const string errorMessage = "Authentication failed";
            await _repository.RecordErrorAsync(account.Id, errorMessage, CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.LastErrorUtc);
            Assert.Equal(errorMessage, retrieved.LastErrorMessage);
            Assert.True(retrieved.LastErrorUtc > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task ProviderAccountRepository_RecordSuccessAsync_ClearsErrorState()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount();
            await _repository.AddAsync(account, CancellationToken.None);

            // First record an error to put the account in error state
            await _repository.RecordErrorAsync(account.Id, "some error", CancellationToken.None);

            // Then record success, which should clear the error state
            await _repository.RecordSuccessAsync(account.Id, CancellationToken.None);

            ProviderAccount? retrieved = await _repository.GetAsync(account.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved.LastErrorUtc);
            Assert.Null(retrieved.LastErrorMessage);
            _ = Assert.NotNull(retrieved.LastSuccessfulAuthUtc);
            Assert.True(retrieved.LastSuccessfulAuthUtc > DateTime.UtcNow.AddMinutes(-1));
        }
        [Fact]
        public async Task ProviderAccountRepository_AddAsync_NewAccount_CreatesEnabledDefaultSyncSchedule()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(Guid.NewGuid(), "Ring");

            await _repository.AddAsync(account, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            SyncSchedule schedule = await db.SyncSchedules.SingleAsync(s => s.ProviderAccountId == account.Id);
            Assert.True(schedule.IsEnabled);
            Assert.Null(schedule.EventNextRunUtc); // never run yet, so the first tick treats it as due
            Assert.True(schedule.EventPollIntervalMinutes >= SyncSchedule.MinimumPollIntervalMinutes);
        }

        [Fact]
        public async Task ProviderAccountRepository_AddAsync_ExistingScheduleForAccount_DoesNotDuplicate()
        {
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(Guid.NewGuid(), "Ring");
            await _repository.AddAsync(account, CancellationToken.None);

            // Updating an existing account (for example on re-authentication) must not add a second schedule.
            account.LastSuccessfulAuthUtc = DateTime.UtcNow;
            await _repository.UpdateAsync(account, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            Assert.Equal(1, await db.SyncSchedules.CountAsync(s => s.ProviderAccountId == account.Id));
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
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
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

            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
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
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
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
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            ProviderAccount? persistedAccount = await db.ProviderAccounts.FindAsync(new object[] { account.Id }, cancellationToken: CancellationToken.None);
            Assert.NotNull(persistedAccount);
            // The raw ProviderName (with newlines) should be stored in the database
            Assert.Equal(injectedProviderName, persistedAccount.ProviderName);
        }
    }
}
