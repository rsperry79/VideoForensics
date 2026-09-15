using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class UnitOfWorkTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();

            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);

            _unitOfWork = new UnitOfWork(
                _fixture.Factory,
                serviceProvider,
                loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWork_ExecuteAsync_CommitsSuccessfulWork()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            User? retrievedUser = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            ProviderAccount? retrievedAccount = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);

            Assert.NotNull(retrievedUser);
            Assert.NotNull(retrievedAccount);
            Assert.Equal(userId, retrievedUser.Id);
            Assert.Equal(accountId, retrievedAccount.Id);
        }

        [Fact]
        public async Task UnitOfWork_ExecuteAsync_RollsBackOnException()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    User user = TestDataBuilder.BuildUser();
                    user.Id = userId;
                    await context.Users.AddAsync(user, CancellationToken.None);

                    ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                    throw new InvalidOperationException("Simulated failure");
                }, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            var userCount = await ctx.Users.CountAsync(u => u.Id == userId);
            var accountCount = await ctx.ProviderAccounts.CountAsync(pa => pa.Id == accountId);

            Assert.Equal(0, userCount);
            Assert.Equal(0, accountCount);
        }

        [Fact]
        public async Task UnitOfWork_ExecuteAsync_ReturnsWorkResult()
        {
            var expectedResult = "test_result";

            var result = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                await context.Users.AddAsync(user, CancellationToken.None);
                return expectedResult;
            }, CancellationToken.None);

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task UnitOfWork_MultipleExecutes_IsolateChanges()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            User user1 = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId1;
                await context.Users.AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            User user2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId2;
                await context.Users.AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            var count = await ctx.Users.CountAsync();
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task UnitOfWork_ActionLogChaining_AcrossMultipleExecutes()
        {
            ActionLogEntry entry1 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ActionLog.AppendAsync(
                    "A1", ActorType.Human, "Act1", "Ent", null, null, CancellationToken.None);
            }, CancellationToken.None);

            ActionLogEntry entry2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ActionLog.AppendAsync(
                    "A2", ActorType.Human, "Act2", "Ent", null, null, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ActionLogEntry? retrieved1 = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == entry1.Id);
            ActionLogEntry? retrieved2 = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == entry2.Id);

            Assert.NotNull(retrieved2.PreviousEntryHash);
            Assert.Equal(retrieved1.EntryHash, retrieved2.PreviousEntryHash);
        }

        [Fact]
        public async Task UnitOfWork_ComplexScenario_MultipleEntitiesAndLog()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();

            ActionLogEntry logEntry = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                ActionLogEntry logEntry = await context.ActionLog.AppendAsync(
                    "TestUser", ActorType.Human, "UserAndLocationCreated", "Location", locationId, null, CancellationToken.None);

                return logEntry;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            User? user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            ProviderAccount? account = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);
            Location? location = await ctx.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            ActionLogEntry? log = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == logEntry.Id);

            Assert.NotNull(user);
            Assert.NotNull(account);
            Assert.NotNull(location);
            Assert.NotNull(log);
            Assert.Equal("UserAndLocationCreated", log.Action);
        }

        [Fact]
        public async Task UnitOfWork_PartialFailure_RollsBackAll()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    User user = TestDataBuilder.BuildUser();
                    user.Id = userId;
                    await context.Users.AddAsync(user, CancellationToken.None);

                    ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                    throw new DbUpdateException("Simulated constraint violation");
                }, CancellationToken.None);
            }
            catch (DbUpdateException)
            {
            }

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            var userExists = await ctx.Users.AnyAsync(u => u.Id == userId);
            var accountExists = await ctx.ProviderAccounts.AnyAsync(pa => pa.Id == accountId);

            Assert.False(userExists);
            Assert.False(accountExists);
        }

        [Fact]
        public async Task EnsureLocation_CalledTwiceWithSamePrimaryKey_ReturnsSameLocationId()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var providerLocationId = "location-123";
            var locationName = "Front Door";
            var address = "123 Main St";

            // Create user and account first
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureLocation call - should create
            Location? location1 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                #pragma warning disable CS0618 // Testing deprecated method's own behavior for backward-compat coverage
                IReadOnlyList<Location> locations = await context.Locations.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
                #pragma warning restore CS0618
                Location? existing = locations.FirstOrDefault(l => l.ProviderLocationId == providerLocationId);

                if (existing == null)
                {
                    var location = new Location
                    {
                        Id = Guid.NewGuid(),
                        ProviderLocationId = providerLocationId,
                        Name = locationName,
                        Address = address
                    };
                    await context.Locations.AddAsync(location, CancellationToken.None);
                    location1 = location;
                }
                else
                {
                    location1 = existing;
                }

                return location1;
            }, CancellationToken.None);

            // Second EnsureLocation call - should find existing
            Location? location2 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                #pragma warning disable CS0618 // Testing deprecated method's own behavior for backward-compat coverage
                IReadOnlyList<Location> locations = await context.Locations.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
                #pragma warning restore CS0618
                Location? existing = locations.FirstOrDefault(l => l.ProviderLocationId == providerLocationId);

                if (existing == null)
                {
                    var location = new Location
                    {
                        Id = Guid.NewGuid(),
                        ProviderLocationId = providerLocationId,
                        Name = locationName,
                        Address = address
                    };
                    await context.Locations.AddAsync(location, CancellationToken.None);
                    location2 = location;
                }
                else
                {
                    location2 = existing;
                }

                return location2;
            }, CancellationToken.None);

            // Assert both calls returned the same location ID
            Assert.NotNull(location1);
            Assert.NotNull(location2);
            Assert.Equal(location1.Id, location2.Id);

            // Verify only one location exists in database
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            var locationCount = await ctx.Locations.CountAsync(l => l.ProviderLocationId == providerLocationId);
            Assert.Equal(1, locationCount);
        }

        [Fact]
        public async Task EnsureDevice_CalledTwiceWithSamePrimaryKey_ReturnsSameDeviceIdAndUpdatesFields()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var providerDeviceId = "device-456";
            var initialName = "Front Camera";
            var updatedName = "Front Camera Updated";
            var initialType = "camera";
            var updatedType = "doorbell";

            // Create user, account, and location first
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureDevice call - should create
            Device? device1 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                IReadOnlyList<Device> devices = await context.Devices.GetByLocationIdAsync(locationId, CancellationToken.None);
                Device? existing = devices.FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);

                if (existing == null)
                {
                    var device = new Device
                    {
                        Id = Guid.NewGuid(),
                        LocationId = locationId,
                        ProviderDeviceId = providerDeviceId,
                        Name = initialName,
                        Type = initialType,
                        IsOnline = true
                    };
                    await context.Devices.AddAsync(device, CancellationToken.None);
                    device1 = device;
                }
                else
                {
                    device1 = existing;
                }

                return device1;
            }, CancellationToken.None);

            // Second EnsureDevice call - should find and update
            Device? device2 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                IReadOnlyList<Device> devices = await context.Devices.GetByLocationIdAsync(locationId, CancellationToken.None);
                Device? existing = devices.FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);

                if (existing == null)
                {
                    var device = new Device
                    {
                        Id = Guid.NewGuid(),
                        LocationId = locationId,
                        ProviderDeviceId = providerDeviceId,
                        Name = updatedName,
                        Type = updatedType,
                        IsOnline = false
                    };
                    await context.Devices.AddAsync(device, CancellationToken.None);
                    device2 = device;
                }
                else
                {
                    // Update existing device
                    existing.Name = updatedName;
                    existing.Type = updatedType;
                    existing.IsOnline = false;
                    await context.Devices.UpdateAsync(existing, CancellationToken.None);
                    device2 = existing;
                }

                return device2;
            }, CancellationToken.None);

            // Assert both calls returned the same device ID
            Assert.NotNull(device1);
            Assert.NotNull(device2);
            Assert.Equal(device1.Id, device2.Id);

            // Verify device was updated in database
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Device? retrievedDevice = await ctx.Devices.FirstOrDefaultAsync(d => d.ProviderDeviceId == providerDeviceId);
            Assert.NotNull(retrievedDevice);
            Assert.Equal(updatedName, retrievedDevice.Name);
            Assert.Equal(updatedType, retrievedDevice.Type);
            Assert.False(retrievedDevice.IsOnline);

            // Verify only one device exists in database
            var deviceCount = await ctx.Devices.CountAsync(d => d.ProviderDeviceId == providerDeviceId);
            Assert.Equal(1, deviceCount);
        }

        private class TestServiceProvider : IServiceProvider
        {
            private readonly SqliteInMemoryFixture _fixture;
            private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

            public TestServiceProvider(SqliteInMemoryFixture fixture, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
            {
                _fixture = fixture;
                _loggerFactory = loggerFactory;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(ICredentialEncryptionProvider))
                {
                    return _fixture.EncryptionProvider;
                }

                if (serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>))
                {
                    return _loggerFactory.CreateLogger<ICredentialRepository>();
                }

                return serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>) ? _loggerFactory.CreateLogger<UnitOfWork>() : (object?)null;
            }
        }
    }

    // Tests for UnitOfWorkUserRepository
    public class UnitOfWorkUserRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_AddAsync_StoresUser()
        {
            var userId = Guid.NewGuid();
            User? createdUser = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            User? retrieved = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(retrieved);
            Assert.Equal(userId, retrieved.Id);
            Assert.Equal(createdUser.ProviderUserKey, retrieved.ProviderUserKey);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_GetAsync_ReturnsUser()
        {
            var userId = Guid.NewGuid();
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            User? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Users.GetAsync(userId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(userId, result.Id);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_GetByProviderKeyAsync_ReturnsUser()
        {
            var providerKey = $"provider_key_{Guid.NewGuid()}";
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser(providerKey);
                await context.Users.AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            User? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Users.GetByProviderKeyAsync(providerKey, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(providerKey, result.ProviderUserKey);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_ListAsync_ReturnsAllUsers()
        {
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user1 = TestDataBuilder.BuildUser();
                user1.Id = user1Id;
                User user2 = TestDataBuilder.BuildUser();
                user2.Id = user2Id;
                await context.Users.AddAsync(user1, CancellationToken.None);
                await context.Users.AddAsync(user2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<User> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Users.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.True(results.Any(u => u.Id == user1Id));
            Assert.True(results.Any(u => u.Id == user2Id));
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_UpdateAsync_ModifiesUser()
        {
            var userId = Guid.NewGuid();
            var newDisplayName = "Updated Name";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User? user = await context.Users.GetAsync(userId, CancellationToken.None);
                if (user != null)
                {
                    user.DisplayName = newDisplayName;
                    await context.Users.UpdateAsync(user, CancellationToken.None);
                }
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            User? retrieved = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(retrieved);
            Assert.Equal(newDisplayName, retrieved.DisplayName);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_DeleteAsync_RemovesUser()
        {
            var userId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Users.DeleteAsync(userId, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            User? retrieved = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            Assert.Null(retrieved);
        }
    }

    // Tests for UnitOfWorkProviderAccountRepository
    public class UnitOfWorkProviderAccountRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_AddAsync_StoresAccount()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ProviderAccount? retrieved = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);
            Assert.NotNull(retrieved);
            Assert.Equal(userId, retrieved.UserId);
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_GetByUserIdAsync_ReturnsAccountsForUser()
        {
            var userId = Guid.NewGuid();
            var accountId1 = Guid.NewGuid();
            var accountId2 = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                account1.Id = accountId1;
                ProviderAccount account2 = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
                account2.Id = accountId2;
                await context.ProviderAccounts.AddAsync(account1, CancellationToken.None);
                await context.ProviderAccounts.AddAsync(account2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderAccount> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ProviderAccounts.GetByUserIdAsync(userId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(a => a.Id == accountId1 && a.ProviderName == "Ring"));
            Assert.True(results.Any(a => a.Id == accountId2 && a.ProviderName == "Wyze"));
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_GetByUserAndProviderAsync_ReturnsAccount()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            ProviderAccount? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ProviderAccounts.GetByUserAndProviderAsync(userId, "Ring", CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(accountId, result.Id);
            Assert.Equal("Ring", result.ProviderName);
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_ListActiveAsync_ReturnsOnlyActiveAccounts()
        {
            var userId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount active = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                active.IsActive = true;
                ProviderAccount inactive = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
                inactive.IsActive = false;
                await context.ProviderAccounts.AddAsync(active, CancellationToken.None);
                await context.ProviderAccounts.AddAsync(inactive, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderAccount> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ProviderAccounts.ListActiveAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.True(results.All(a => a.IsActive));
            Assert.True(results.Any(a => a.ProviderName == "Ring"));
            Assert.False(results.Any(a => a.ProviderName == "Wyze"));
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_UpdateAsync_ModifiesAccount()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                account.IsActive = true;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderAccount? account = await context.ProviderAccounts.GetAsync(accountId, CancellationToken.None);
                if (account != null)
                {
                    account.IsActive = false;
                    await context.ProviderAccounts.UpdateAsync(account, CancellationToken.None);
                }
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ProviderAccount? retrieved = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.IsActive);
        }
    }

    // Tests for UnitOfWorkLocationRepository
    public class UnitOfWorkLocationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_GetByProviderLocationIdAsync_ReturnsLocation()
        {
            var providerLocationId = $"loc_{Guid.NewGuid()}";
            var locationId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation(providerLocationId);
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Location? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Locations.GetByProviderLocationIdAsync(providerLocationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(locationId, result.Id);
            Assert.Equal(providerLocationId, result.ProviderLocationId);
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_GetByApiHashAsync_ReturnsLocation()
        {
            var apiHash = $"hash_{Guid.NewGuid()}";
            var locationId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                location.ApiResponseHash = apiHash;
                await context.Locations.AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Location? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Locations.GetByApiHashAsync(apiHash, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(locationId, result.Id);
            Assert.Equal(apiHash, result.ApiResponseHash);
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_ListAsync_ReturnsAllLocations()
        {
            var loc1Id = Guid.NewGuid();
            var loc2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location loc1 = TestDataBuilder.BuildLocation();
                loc1.Id = loc1Id;
                Location loc2 = TestDataBuilder.BuildLocation();
                loc2.Id = loc2Id;
                await context.Locations.AddAsync(loc1, CancellationToken.None);
                await context.Locations.AddAsync(loc2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Location> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Locations.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.True(results.Any(l => l.Id == loc1Id));
            Assert.True(results.Any(l => l.Id == loc2Id));
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_UpdateAsync_ModifiesLocation()
        {
            var locationId = Guid.NewGuid();
            var newName = "Updated Location Name";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location? location = await context.Locations.GetAsync(locationId, CancellationToken.None);
                if (location != null)
                {
                    location.Name = newName;
                    await context.Locations.UpdateAsync(location, CancellationToken.None);
                }
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Location? retrieved = await ctx.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            Assert.NotNull(retrieved);
            Assert.Equal(newName, retrieved.Name);
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_DeleteAsync_RemovesLocation()
        {
            var locationId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Locations.DeleteAsync(locationId, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Location? retrieved = await ctx.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            Assert.Null(retrieved);
        }
    }

    // Tests for UnitOfWorkDeviceRepository
    public class UnitOfWorkDeviceRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_GetByProviderDeviceIdAsync_ReturnsDevice()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var providerDeviceId = $"dev_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId, providerDeviceId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Device? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Devices.GetByProviderDeviceIdAsync(locationId, providerDeviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.Id);
            Assert.Equal(providerDeviceId, result.ProviderDeviceId);
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_GetByLocationIdAsync_ReturnsDevicesForLocation()
        {
            var locationId = Guid.NewGuid();
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device1 = TestDataBuilder.BuildDevice(locationId);
                device1.Id = device1Id;
                Device device2 = TestDataBuilder.BuildDevice(locationId);
                device2.Id = device2Id;
                await context.Devices.AddAsync(device1, CancellationToken.None);
                await context.Devices.AddAsync(device2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Device> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Devices.GetByLocationIdAsync(locationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(d => d.Id == device1Id));
            Assert.True(results.Any(d => d.Id == device2Id));
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_GetByApiHashAsync_ReturnsDevice()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var apiHash = $"hash_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                device.ApiResponseHash = apiHash;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Device? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Devices.GetByApiHashAsync(apiHash, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(deviceId, result.Id);
            Assert.Equal(apiHash, result.ApiResponseHash);
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_UpdateLastSuccessfulPullAsync_UpdatesTimestamp()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var pulledTime = DateTime.UtcNow;

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Devices.UpdateLastSuccessfulPullAsync(deviceId, pulledTime, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Device? retrieved = await ctx.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            Assert.NotNull(retrieved);
            Assert.True((pulledTime - retrieved.LastSuccessfulPullAtUtc!.Value).TotalMilliseconds < 1000);
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_ListAsync_ReturnsAllDevices()
        {
            var loc1Id = Guid.NewGuid();
            var loc2Id = Guid.NewGuid();
            var dev1Id = Guid.NewGuid();
            var dev2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location loc1 = TestDataBuilder.BuildLocation();
                loc1.Id = loc1Id;
                Location loc2 = TestDataBuilder.BuildLocation();
                loc2.Id = loc2Id;
                await context.Locations.AddAsync(loc1, CancellationToken.None);
                await context.Locations.AddAsync(loc2, CancellationToken.None);

                Device dev1 = TestDataBuilder.BuildDevice(loc1Id);
                dev1.Id = dev1Id;
                Device dev2 = TestDataBuilder.BuildDevice(loc2Id);
                dev2.Id = dev2Id;
                await context.Devices.AddAsync(dev1, CancellationToken.None);
                await context.Devices.AddAsync(dev2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Device> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Devices.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.True(results.Count >= 2);
            Assert.True(results.Any(d => d.Id == dev1Id));
            Assert.True(results.Any(d => d.Id == dev2Id));
        }
    }

    // Tests for UnitOfWorkMediaItemRepository
    public class UnitOfWorkMediaItemRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkMediaItemRepository_GetByDeviceIdAsync_ReturnsMediaItems()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var media1Id = Guid.NewGuid();
            var media2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media1 = TestDataBuilder.BuildMediaItem(deviceId);
                media1.Id = media1Id;
                MediaItem media2 = TestDataBuilder.BuildMediaItem(deviceId);
                media2.Id = media2Id;
                await context.MediaItems.AddAsync(media1, CancellationToken.None);
                await context.MediaItems.AddAsync(media2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<MediaItem> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.MediaItems.GetByDeviceIdAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(m => m.Id == media1Id));
            Assert.True(results.Any(m => m.Id == media2Id));
        }

        [Fact]
        public async Task UnitOfWorkMediaItemRepository_GetByHashAsync_ReturnsMediaItem()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            var hash = $"hash_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId, null, null, hash);
                media.Id = mediaId;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            MediaItem? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.MediaItems.GetByHashAsync(hash, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(mediaId, result.Id);
            Assert.Equal(hash, result.Sha256Hash);
        }

        [Fact]
        public async Task UnitOfWorkMediaItemRepository_GetByApiSourceHashAsync_ReturnsMediaItem()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            var apiHash = $"apihash_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                media.ApiSourceHash = apiHash;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            MediaItem? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.MediaItems.GetByApiSourceHashAsync(apiHash, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(mediaId, result.Id);
            Assert.Equal(apiHash, result.ApiSourceHash);
        }

        [Fact]
        public async Task UnitOfWorkMediaItemRepository_GetByDownloadEventIdAsync_ReturnsMediaItems()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            var media1Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId);
                media.Id = media1Id;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<MediaItem> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.MediaItems.GetByDownloadEventIdAsync(downloadEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.True(results.Any(m => m.Id == media1Id && m.DownloadEventId == downloadEventId));
        }

        [Fact]
        public async Task UnitOfWorkMediaItemRepository_UpdateAsync_ModifiesMediaItem()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            var newFileName = "updated_video.mp4";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                MediaItem? media = await context.MediaItems.GetAsync(mediaId, CancellationToken.None);
                if (media != null)
                {
                    media.FileName = newFileName;
                    await context.MediaItems.UpdateAsync(media, CancellationToken.None);
                }
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            MediaItem? retrieved = await ctx.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaId);
            Assert.NotNull(retrieved);
            Assert.Equal(newFileName, retrieved.FileName);
        }
    }

    // Tests for UnitOfWorkDownloadEventRepository
    public class UnitOfWorkDownloadEventRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkDownloadEventRepository_GetByDeviceIdAsync_ReturnsDownloadEvents()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var event1Id = Guid.NewGuid();
            var event2Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                DownloadEvent evt1 = TestDataBuilder.BuildDownloadEvent(deviceId);
                evt1.Id = event1Id;
                DownloadEvent evt2 = TestDataBuilder.BuildDownloadEvent(deviceId);
                evt2.Id = event2Id;
                await context.DownloadEvents.AddAsync(evt1, CancellationToken.None);
                await context.DownloadEvents.AddAsync(evt2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DownloadEvent> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DownloadEvents.GetByDeviceIdAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(e => e.Id == event1Id));
            Assert.True(results.Any(e => e.Id == event2Id));
        }

        [Fact]
        public async Task UnitOfWorkDownloadEventRepository_GetByProviderEventIdAsync_ReturnsDownloadEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var providerEventId = $"evt_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                DownloadEvent evt = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId);
                evt.Id = eventId;
                await context.DownloadEvents.AddAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DownloadEvent? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DownloadEvents.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(eventId, result.Id);
            Assert.Equal(providerEventId, result.ProviderEventId);
        }

        [Fact]
        public async Task UnitOfWorkDownloadEventRepository_ExistsForProviderEventIdAsync_ReturnsTrueWhenExists()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var providerEventId = $"evt_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                DownloadEvent evt = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId);
                await context.DownloadEvents.AddAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            bool exists = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DownloadEvents.ExistsForProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.True(exists);
        }

        [Fact]
        public async Task UnitOfWorkDownloadEventRepository_GetLatestSuccessfulEventTimeAsync_ReturnsLatest()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                DownloadEvent evt1 = TestDataBuilder.BuildDownloadEvent(deviceId, $"evt_1", true);
                evt1.EventOccurredAtUtc = now.AddHours(-2);
                DownloadEvent evt2 = TestDataBuilder.BuildDownloadEvent(deviceId, $"evt_2", true);
                evt2.EventOccurredAtUtc = now.AddHours(-1);
                await context.DownloadEvents.AddAsync(evt1, CancellationToken.None);
                await context.DownloadEvents.AddAsync(evt2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DateTime? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DownloadEvents.GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True((now.AddHours(-1) - result.Value).TotalMinutes < 1);
        }
    }

    // Tests for UnitOfWorkCredentialRepository
    public class UnitOfWorkCredentialRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_SetAsync_StoresEncryptedCredential()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var plainValue = "my_secret_token";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.SetAsync(accountId, "AccessToken", plainValue, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Credential? stored = await ctx.Credentials.FirstOrDefaultAsync(c => c.ProviderAccountId == accountId && c.CredentialType == "AccessToken");
            Assert.NotNull(stored);
            Assert.NotEqual(plainValue, stored.EncryptedValue);
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_GetAsync_DecryptsCredential()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var plainValue = "my_secret_token";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.SetAsync(accountId, "AccessToken", plainValue, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            var result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Credentials.GetAsync(accountId, "AccessToken", CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(plainValue, result.Value.DecryptedValue);
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_GetByProviderAccountIdAsync_ReturnsCredentials()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                await context.Credentials.SetAsync(accountId, "RefreshToken", "refresh_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Credential> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Credentials.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(c => c.CredentialType == "AccessToken"));
            Assert.True(results.Any(c => c.CredentialType == "RefreshToken"));
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_DeleteAsync_RemovesCredential()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.DeleteAsync(accountId, "AccessToken", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Credential? retrieved = await ctx.Credentials.FirstOrDefaultAsync(c => c.ProviderAccountId == accountId && c.CredentialType == "AccessToken");
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_DeleteByProviderAccountIdAsync_RemovesAllCredentials()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                await context.Credentials.SetAsync(accountId, "RefreshToken", "refresh_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Credentials.DeleteByProviderAccountIdAsync(accountId, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int credentialCount = await ctx.Credentials.CountAsync(c => c.ProviderAccountId == accountId);
            Assert.Equal(0, credentialCount);
        }
    }

    // Tests for UnitOfWorkEventRepository
    public class UnitOfWorkEventRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_CreateAsync_StoresEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event createdEvent = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.Id = eventId;
                return await context.Events.CreateAsync(evt, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Event? retrieved = await ctx.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            Assert.NotNull(retrieved);
            Assert.Equal(eventId, retrieved.Id);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_UpsertAsync_InsertsNewEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var providerEventId = $"evt_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event upsertedEvent = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                return await context.Events.UpsertAsync(evt, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Event? retrieved = await ctx.Events.FirstOrDefaultAsync(e => e.ProviderEventId == providerEventId);
            Assert.NotNull(retrieved);
            Assert.Equal(upsertedEvent.Id, retrieved.Id);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_UpsertAsync_UpdatesExistingEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var providerEventId = $"evt_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                evt.EventType = "Motion";
                await context.Events.CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event updated = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                evt.EventType = "Person";
                return await context.Events.UpsertAsync(evt, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Event? retrieved = await ctx.Events.FirstOrDefaultAsync(e => e.ProviderEventId == providerEventId);
            Assert.NotNull(retrieved);
            Assert.Equal("Person", retrieved.EventType);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_GetByProviderEventIdAsync_ReturnsEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var providerEventId = $"evt_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                await context.Events.CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Events.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(providerEventId, result.ProviderEventId);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_GetByApiSourceHashAsync_ReturnsEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var apiHash = $"apihash_{Guid.NewGuid()}";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.ApiSourceHash = apiHash;
                await context.Events.CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.Events.GetByApiSourceHashAsync(apiHash, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(apiHash, result.ApiSourceHash);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_UpdateDownloadFailureAsync_SetsFailureStatus()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var failureTime = DateTime.UtcNow;

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.Id = eventId;
                await context.Events.CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.Events.UpdateDownloadFailureAsync(eventId, failureTime, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Event? retrieved = await ctx.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            Assert.NotNull(retrieved);
            Assert.Equal(EventDownloadStatus.DownloadFailed, retrieved.DownloadStatus);
        }
    }

    // Tests for UnitOfWorkDeviceConfigRepository
    public class UnitOfWorkDeviceConfigRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_AppendSnapshotAsync_StoresSnapshot()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var snapshotId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync(async context =>
            {
                DeviceConfigSnapshot snapshot = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snapshot.Id = snapshotId;
                return await context.DeviceConfig.AppendSnapshotAsync(snapshot, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            DeviceConfigSnapshot? retrieved = await ctx.DeviceConfigSnapshots.FirstOrDefaultAsync(s => s.Id == snapshotId);
            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_GetLatestAsync_ReturnsNewestSnapshot()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DateTime now = DateTime.UtcNow;
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snap1.CapturedAtUtc = now.AddHours(-2);
                await context.DeviceConfig.AppendSnapshotAsync(snap1, CancellationToken.None);

                DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snap2.CapturedAtUtc = now;
                await context.DeviceConfig.AppendSnapshotAsync(snap2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DeviceConfigSnapshot? latest = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DeviceConfig.GetLatestAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(latest);
            Assert.True((now - latest.CapturedAtUtc).TotalSeconds < 1);
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_GetHistoryAsync_ReturnsAllSnapshots()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                await context.DeviceConfig.AppendSnapshotAsync(snap1, CancellationToken.None);

                DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                await context.DeviceConfig.AppendSnapshotAsync(snap2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> history = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DeviceConfig.GetHistoryAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_ListAsync_ReturnsAllSnapshots()
        {
            var loc1Id = Guid.NewGuid();
            var dev1Id = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = loc1Id;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(loc1Id);
                device.Id = dev1Id;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(dev1Id);
                await context.DeviceConfig.AppendSnapshotAsync(snap1, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.DeviceConfig.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
        }
    }

    // Tests for UnitOfWorkProviderReconciliationRepository
    public class UnitOfWorkProviderReconciliationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkProviderReconciliationRepository_AppendAsync_StoresRecord()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var recordId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync(async context =>
            {
                ProviderReconciliationRecord record = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                record.Id = recordId;
                return await context.ProviderReconciliation.AppendAsync(record, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ProviderReconciliationRecord? retrieved = await ctx.ProviderReconciliationRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
        }

        [Fact]
        public async Task UnitOfWorkProviderReconciliationRepository_GetHistoryForDeviceAsync_ReturnsRecords()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderReconciliationRecord record1 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                ProviderReconciliationRecord record2 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                await context.ProviderReconciliation.AppendAsync(record1, CancellationToken.None);
                await context.ProviderReconciliation.AppendAsync(record2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderReconciliationRecord> history = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ProviderReconciliation.GetHistoryForDeviceAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task UnitOfWorkProviderReconciliationRepository_ListAsync_ReturnsAllRecords()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderReconciliationRecord record = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                await context.ProviderReconciliation.AppendAsync(record, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderReconciliationRecord> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ProviderReconciliation.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
        }
    }

    // Tests for UnitOfWorkExportRecordRepository
    public class UnitOfWorkExportRecordRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkExportRecordRepository_AppendAsync_StoresRecordAndItems()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            var recordId = Guid.NewGuid();
            var itemId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                record.Id = recordId;

                ExportRecordItem item = TestDataBuilder.BuildExportRecordItem(recordId, mediaId);
                item.Id = itemId;

                return await context.ExportRecords.AppendAsync(record, new List<ExportRecordItem> { item }, CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ExportRecord? retrieved = await ctx.ExportRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            Assert.NotNull(retrieved);

            ExportRecordItem? retrievedItem = await ctx.ExportRecordItems.FirstOrDefaultAsync(i => i.Id == itemId);
            Assert.NotNull(retrievedItem);
            Assert.Equal(recordId, retrievedItem.ExportRecordId);
        }

        [Fact]
        public async Task UnitOfWorkExportRecordRepository_GetItemsForRecordAsync_ReturnsItems()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            var recordId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await context.Devices.AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await context.MediaItems.AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                record.Id = recordId;

                ExportRecordItem item = TestDataBuilder.BuildExportRecordItem(recordId, mediaId);
                await context.ExportRecords.AppendAsync(record, new List<ExportRecordItem> { item }, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportRecordItem> items = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportRecords.GetItemsForRecordAsync(recordId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(items);
            Assert.True(items.Any(i => i.MediaItemId == mediaId));
        }

        [Fact]
        public async Task UnitOfWorkExportRecordRepository_ListAsync_ReturnsAllRecords()
        {
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                await context.ExportRecords.AppendAsync(record, new List<ExportRecordItem>(), CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportRecord> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportRecords.ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
        }
    }

    // Tests for UnitOfWorkAccessAuditLogRepository
    public class UnitOfWorkAccessAuditLogRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_RecordAccessAsync_StoresEntry()
        {
            var evidenceId = Guid.NewGuid();
            var userId = $"user_{Guid.NewGuid()}";

            AccessAuditLogEntity recorded = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.AccessAuditLogs.RecordAccessAsync(
                    evidenceId, userId, "View", "192.168.1.1", "Investigation", CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            AccessAuditLogEntity? retrieved = await ctx.AccessAuditLogs.FirstOrDefaultAsync(l => l.Id == recorded.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(evidenceId, retrieved.EvidenceId);
            Assert.Equal(userId, retrieved.UserId);
            Assert.Equal("View", retrieved.Action);
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_GetForEvidenceAsync_ReturnsEntries()
        {
            var evidenceId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.AccessAuditLogs.RecordAccessAsync(
                    evidenceId, "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                await context.AccessAuditLogs.RecordAccessAsync(
                    evidenceId, "user2", "Download", "192.168.1.2", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.AccessAuditLogs.GetForEvidenceAsync(evidenceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, entries.Count);
            Assert.True(entries.Any(e => e.UserId == "user1" && e.Action == "View"));
            Assert.True(entries.Any(e => e.UserId == "user2" && e.Action == "Download"));
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_GetByUserAsync_ReturnsUserEntries()
        {
            var userId = "testuser";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.AccessAuditLogs.RecordAccessAsync(
                    Guid.NewGuid(), userId, "View", "192.168.1.1", "Investigation", CancellationToken.None);
                await context.AccessAuditLogs.RecordAccessAsync(
                    Guid.NewGuid(), userId, "Download", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.AccessAuditLogs.GetByUserAsync(userId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, entries.Count);
            Assert.True(entries.All(e => e.UserId == userId));
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_GetByDateRangeAsync_ReturnsEntriesInRange()
        {
            DateTime now = DateTime.UtcNow;
            DateTime fromUtc = now.AddHours(-1);
            DateTime toUtc = now.AddHours(1);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.AccessAuditLogs.RecordAccessAsync(
                    Guid.NewGuid(), "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.AccessAuditLogs.GetByDateRangeAsync(fromUtc, toUtc, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(entries);
            Assert.True(entries.All(e => e.AccessedAtUtc >= fromUtc && e.AccessedAtUtc <= toUtc));
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_ListAsync_ReturnsPaginatedResults()
        {
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.AccessAuditLogs.RecordAccessAsync(
                    Guid.NewGuid(), "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.AccessAuditLogs.ListAsync(0, 10, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(entries);
        }
    }

    // Tests for UnitOfWorkExportAuditRecordRepository
    public class UnitOfWorkExportAuditRecordRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_RecordExportAsync_StoresRecord()
        {
            var locationId = Guid.NewGuid();

            ExportAuditRecordEntity recorded = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.RecordExportAsync(
                    locationId, "TestUser", 10, "AES256Archive", "CaseFile", CancellationToken.None);
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            ExportAuditRecordEntity? retrieved = await ctx.ExportAuditRecords.FirstOrDefaultAsync(r => r.Id == recorded.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(locationId, retrieved.LocationId);
            Assert.Equal("TestUser", retrieved.ExportedBy);
            Assert.Equal(10, retrieved.EventsExported);
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetForLocationAsync_ReturnsLocationRecords()
        {
            var locationId = Guid.NewGuid();

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.ExportAuditRecords.RecordExportAsync(
                    locationId, "user1", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                await context.ExportAuditRecords.RecordExportAsync(
                    locationId, "user2", 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.GetForLocationAsync(locationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, records.Count);
            Assert.True(records.All(r => r.LocationId == locationId));
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetByUserAsync_ReturnsUserRecords()
        {
            var exportedBy = "TestUser";

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), exportedBy, 10, "AES256Archive", "CaseFile", CancellationToken.None);
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), exportedBy, 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.GetByUserAsync(exportedBy, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, records.Count);
            Assert.True(records.All(r => r.ExportedBy == exportedBy));
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetByDateRangeAsync_ReturnsRecordsInRange()
        {
            DateTime now = DateTime.UtcNow;
            DateTime fromUtc = now.AddHours(-1);
            DateTime toUtc = now.AddHours(1);

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), "TestUser", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.GetByDateRangeAsync(fromUtc, toUtc, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(records);
            Assert.True(records.All(r => r.ExportedAtUtc >= fromUtc && r.ExportedAtUtc <= toUtc));
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetStatisticsAsync_CalculatesStats()
        {
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), "user1", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), "user2", 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            ExportStatistics stats = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.GetStatisticsAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, stats.TotalExports);
            Assert.Equal(15, stats.TotalEventsExported);
            Assert.Equal(2, stats.UniqueExporters);
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_ListAsync_ReturnsPaginatedResults()
        {
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await context.ExportAuditRecords.RecordExportAsync(
                    Guid.NewGuid(), "TestUser", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ExportAuditRecords.ListAsync(0, 10, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(records);
        }
    }

    internal class TestServiceProvider : IServiceProvider
    {
        private readonly SqliteInMemoryFixture _fixture;
        private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

        public TestServiceProvider(SqliteInMemoryFixture fixture, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _fixture = fixture;
            _loggerFactory = loggerFactory;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ICredentialEncryptionProvider))
            {
                return _fixture.EncryptionProvider;
            }

            if (serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>))
            {
                return _loggerFactory.CreateLogger<ICredentialRepository>();
            }

            return serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>) ? _loggerFactory.CreateLogger<UnitOfWork>() : (object?)null;
        }
    }
}
