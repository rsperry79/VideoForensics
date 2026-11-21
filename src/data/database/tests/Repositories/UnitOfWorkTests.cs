using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
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
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

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
        public async Task UnitOfWork_DeviceSettingChangeAddRange_RollsBackWithDeviceUpdate()
        {
            // Change history must commit or roll back with the Device write it describes. The writes are flushed with
            // SaveChangesAsync before the throw, so the rollback is proven against rows that actually reached the database.
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            const string originalFirmware = "1.0.0";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                device.FirmwareVersion = originalFirmware;
                device.LightBrightness = 1;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Device device = (await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).GetAsync(deviceId, CancellationToken.None))!;
                device.FirmwareVersion = "2.0.0";
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).UpdateAsync(device, CancellationToken.None);
                await new DeviceSettingChangeRepository(_unitOfWork, NullLogger<DeviceSettingChangeRepository>.Instance).AddRangeAsync(
                [
                    new DeviceSettingChange { Id = Guid.NewGuid(), DeviceId = deviceId, FieldName = "LightBrightness", OldValue = "1", NewValue = "2", ChangedAtUtc = DateTime.UtcNow }
                ], CancellationToken.None);

                // Flush both writes to the database inside the transaction, then fail before commit.
                await context.SaveChangesAsync(CancellationToken.None);
                throw new InvalidOperationException("boom");
            }, CancellationToken.None));

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Device? stored = await ctx.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            Assert.NotNull(stored);
            Assert.Equal(originalFirmware, stored.FirmwareVersion);
            Assert.Equal(0, await ctx.DeviceSettingChanges.CountAsync(c => c.DeviceId == deviceId));
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
                    await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                    ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                    throw new InvalidOperationException("Simulated failure");
                }, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int userCount = await ctx.Users.CountAsync(u => u.Id == userId);
            int accountCount = await ctx.ProviderAccounts.CountAsync(pa => pa.Id == accountId);

            Assert.Equal(0, userCount);
            Assert.Equal(0, accountCount);
        }

        [Fact]
        public async Task UnitOfWork_ExecuteAsync_ReturnsWorkResult()
        {
            string expectedResult = "test_result";

            string result = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
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
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            User user2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId2;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            int count = await ctx.Users.CountAsync();
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task UnitOfWork_ActionLogChaining_AcrossMultipleExecutes()
        {
            ActionLogEntry entry1 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ActionLogRepository(_unitOfWork, NullLogger<ActionLogRepository>.Instance).AppendAsync(
                    "A1", ActorType.Human, "Act1", "Ent", null, null, CancellationToken.None);
            }, CancellationToken.None);

            ActionLogEntry entry2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ActionLogRepository(_unitOfWork, NullLogger<ActionLogRepository>.Instance).AppendAsync(
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
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                ActionLogEntry logEntry = await new ActionLogRepository(_unitOfWork, NullLogger<ActionLogRepository>.Instance).AppendAsync(
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
                    await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                    ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                    throw new DbUpdateException("Simulated constraint violation");
                }, CancellationToken.None);
            }
            catch (DbUpdateException)
            {
            }

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            bool userExists = await ctx.Users.AnyAsync(u => u.Id == userId);
            bool accountExists = await ctx.ProviderAccounts.AnyAsync(pa => pa.Id == accountId);

            Assert.False(userExists);
            Assert.False(accountExists);
        }

        [Fact]
        public async Task EnsureLocation_CalledTwiceWithSamePrimaryKey_ReturnsSameLocationId()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            string providerLocationId = "location-123";
            string locationName = "Front Door";
            string address = "123 Main St";

            // Create user and account first
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureLocation call - should create
            Location? location1 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                IReadOnlyList<Location> locations = await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).ListAsync(CancellationToken.None);
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
                    await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);
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
                IReadOnlyList<Location> locations = await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).ListAsync(CancellationToken.None);
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
                    await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);
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
            int locationCount = await ctx.Locations.CountAsync(l => l.ProviderLocationId == providerLocationId);
            Assert.Equal(1, locationCount);
        }

        [Fact]
        public async Task EnsureDevice_CalledTwiceWithSamePrimaryKey_ReturnsSameDeviceIdAndUpdatesFields()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            string providerDeviceId = "device-456";
            string initialName = "Front Camera";
            string updatedName = "Front Camera Updated";
            string initialType = "camera";
            string updatedType = "doorbell";

            // Create user, account, and location first
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);

                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureDevice call - should create
            Device? device1 = null;
            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                IReadOnlyList<Device> devices = await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).GetByLocationIdAsync(locationId, CancellationToken.None);
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
                    await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
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
                IReadOnlyList<Device> devices = await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).GetByLocationIdAsync(locationId, CancellationToken.None);
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
                    await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                    device2 = device;
                }
                else
                {
                    // Update existing device
                    existing.Name = updatedName;
                    existing.Type = updatedType;
                    existing.IsOnline = false;
                    await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).UpdateAsync(existing, CancellationToken.None);
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
            int deviceCount = await ctx.Devices.CountAsync(d => d.ProviderDeviceId == providerDeviceId);
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
                return serviceType == typeof(ICredentialEncryptionProvider)
                    ? _fixture.EncryptionProvider
                    : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>)
                    ? _loggerFactory.CreateLogger<ICredentialRepository>()
                    : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>) ? _loggerFactory.CreateLogger<UnitOfWork>() : (object?)null;
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
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
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
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            User? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).GetAsync(userId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(userId, result.Id);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_GetByProviderKeyAsync_ReturnsUser()
        {
            string providerKey = $"provider_key_{Guid.NewGuid()}";
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser(providerKey);
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            User? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).GetByProviderKeyAsync(providerKey, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(providerKey, result.ProviderUserKey);
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_ListAsync_ReturnsAllUsers()
        {
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user1 = TestDataBuilder.BuildUser();
                user1.Id = user1Id;
                User user2 = TestDataBuilder.BuildUser();
                user2.Id = user2Id;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user1, CancellationToken.None);
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<User> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.True(results.Any(u => u.Id == user1Id));
            Assert.True(results.Any(u => u.Id == user2Id));
        }

        [Fact]
        public async Task UnitOfWorkUserRepository_UpdateAsync_ModifiesUser()
        {
            var userId = Guid.NewGuid();
            string newDisplayName = "Updated Name";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User? user = await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).GetAsync(userId, CancellationToken.None);
                if (user != null)
                {
                    user.DisplayName = newDisplayName;
                    await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).UpdateAsync(user, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).DeleteAsync(userId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account1 = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                account1.Id = accountId1;
                ProviderAccount account2 = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
                account2.Id = accountId2;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account1, CancellationToken.None);
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderAccount> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).GetByUserIdAsync(userId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            ProviderAccount? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).GetByUserAndProviderAsync(userId, "Ring", CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(accountId, result.Id);
            Assert.Equal("Ring", result.ProviderName);
        }

        [Fact]
        public async Task UnitOfWorkProviderAccountRepository_ListActiveAsync_ReturnsOnlyActiveAccounts()
        {
            var userId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount active = TestDataBuilder.BuildProviderAccount(userId, "Ring");
                active.IsActive = true;
                ProviderAccount inactive = TestDataBuilder.BuildProviderAccount(userId, "Wyze");
                inactive.IsActive = false;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(active, CancellationToken.None);
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(inactive, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderAccount> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).ListActiveAsync(CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                account.IsActive = true;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderAccount? account = await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).GetAsync(accountId, CancellationToken.None);
                if (account != null)
                {
                    account.IsActive = false;
                    await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).UpdateAsync(account, CancellationToken.None);
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
            string providerLocationId = $"loc_{Guid.NewGuid()}";
            var locationId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation(providerLocationId);
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Location? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).GetByProviderLocationIdAsync(providerLocationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(locationId, result.Id);
            Assert.Equal(providerLocationId, result.ProviderLocationId);
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_ListAsync_ReturnsAllLocations()
        {
            var loc1Id = Guid.NewGuid();
            var loc2Id = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location loc1 = TestDataBuilder.BuildLocation();
                loc1.Id = loc1Id;
                Location loc2 = TestDataBuilder.BuildLocation();
                loc2.Id = loc2Id;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(loc1, CancellationToken.None);
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(loc2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Location> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).ListAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.True(results.Any(l => l.Id == loc1Id));
            Assert.True(results.Any(l => l.Id == loc2Id));
        }

        [Fact]
        public async Task UnitOfWorkLocationRepository_UpdateAsync_ModifiesLocation()
        {
            var locationId = Guid.NewGuid();
            string newName = "Updated Location Name";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location? location = await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).GetAsync(locationId, CancellationToken.None);
                if (location != null)
                {
                    location.Name = newName;
                    await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).UpdateAsync(location, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).DeleteAsync(locationId, CancellationToken.None);
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
            string providerDeviceId = $"dev_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId, providerDeviceId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Device? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).GetByProviderDeviceIdAsync(locationId, providerDeviceId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device1 = TestDataBuilder.BuildDevice(locationId);
                device1.Id = device1Id;
                Device device2 = TestDataBuilder.BuildDevice(locationId);
                device2.Id = device2Id;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device1, CancellationToken.None);
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Device> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).GetByLocationIdAsync(locationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.True(results.Any(d => d.Id == device1Id));
            Assert.True(results.Any(d => d.Id == device2Id));
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_UpdateLastSuccessfulPullAsync_UpdatesTimestamp()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            DateTime pulledTime = DateTime.UtcNow;

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).UpdateLastSuccessfulPullAsync(deviceId, pulledTime, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location loc1 = TestDataBuilder.BuildLocation();
                loc1.Id = loc1Id;
                Location loc2 = TestDataBuilder.BuildLocation();
                loc2.Id = loc2Id;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(loc1, CancellationToken.None);
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(loc2, CancellationToken.None);

                Device dev1 = TestDataBuilder.BuildDevice(loc1Id);
                dev1.Id = dev1Id;
                Device dev2 = TestDataBuilder.BuildDevice(loc2Id);
                dev2.Id = dev2Id;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(dev1, CancellationToken.None);
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(dev2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Device> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).ListAsync(CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media1 = TestDataBuilder.BuildMediaItem(deviceId);
                media1.Id = media1Id;
                MediaItem media2 = TestDataBuilder.BuildMediaItem(deviceId);
                media2.Id = media2Id;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media1, CancellationToken.None);
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<MediaItem> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).GetByDeviceIdAsync(deviceId, CancellationToken.None);
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
            string hash = $"hash_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId, null, null, hash);
                media.Id = mediaId;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            MediaItem? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).GetByHashAsync(hash, CancellationToken.None);
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
            string apiHash = $"apihash_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                media.ApiSourceHash = apiHash;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            MediaItem? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).GetByApiSourceHashAsync(apiHash, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId);
                media.Id = media1Id;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<MediaItem> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).GetByDownloadEventIdAsync(downloadEventId, CancellationToken.None);
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
            string newFileName = "updated_video.mp4";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                MediaItem? media = await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).GetAsync(mediaId, CancellationToken.None);
                if (media != null)
                {
                    media.FileName = newFileName;
                    await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).UpdateAsync(media, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                DownloadEvent evt1 = TestDataBuilder.BuildDownloadEvent(deviceId);
                evt1.Id = event1Id;
                DownloadEvent evt2 = TestDataBuilder.BuildDownloadEvent(deviceId);
                evt2.Id = event2Id;
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt1, CancellationToken.None);
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DownloadEvent> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).GetByDeviceIdAsync(deviceId, CancellationToken.None);
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
            string providerEventId = $"evt_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                DownloadEvent evt = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId);
                evt.Id = eventId;
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DownloadEvent? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
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
            string providerEventId = $"evt_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                DownloadEvent evt = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId);
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            bool exists = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).ExistsForProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.True(exists);
        }

        [Fact]
        public async Task UnitOfWorkDownloadEventRepository_GetLatestSuccessfulEventTimeAsync_ReturnsLatest()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                DownloadEvent evt1 = TestDataBuilder.BuildDownloadEvent(deviceId, $"evt_1", true);
                evt1.EventOccurredAtUtc = now.AddHours(-2);
                DownloadEvent evt2 = TestDataBuilder.BuildDownloadEvent(deviceId, $"evt_2", true);
                evt2.EventOccurredAtUtc = now.AddHours(-1);
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt1, CancellationToken.None);
                await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).AddAsync(evt2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DateTime? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DownloadEventRepository(_unitOfWork, NullLogger<DownloadEventRepository>.Instance).GetLatestSuccessfulEventTimeAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            _ = Assert.NotNull(result);
            Assert.True((now.AddHours(-1) - result.Value).TotalMinutes < 1);
        }
    }

    // Tests for UnitOfWorkCredentialRepository
    public class UnitOfWorkCredentialRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private ICredentialEncryptionProvider _credentialEncryption = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
            _credentialEncryption = serviceProvider.GetRequiredService<ICredentialEncryptionProvider>();
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
            string plainValue = "my_secret_token";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "AccessToken", plainValue, CancellationToken.None);
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
            string plainValue = "my_secret_token";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "AccessToken", plainValue, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            (string CredentialType, string DecryptedValue)? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).GetAsync(accountId, "AccessToken", CancellationToken.None);
            }, CancellationToken.None);

            _ = Assert.NotNull(result);
            Assert.Equal(plainValue, result.Value.DecryptedValue);
        }

        [Fact]
        public async Task UnitOfWorkCredentialRepository_GetByProviderAccountIdAsync_ReturnsCredentials()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "RefreshToken", "refresh_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<Credential> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).GetByProviderAccountIdAsync(accountId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).DeleteAsync(accountId, "AccessToken", CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                User user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await new UserRepository(_unitOfWork, NullLogger<UserRepository>.Instance).AddAsync(user, CancellationToken.None);

                ProviderAccount account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await new ProviderAccountRepository(_unitOfWork, NullLogger<ProviderAccountRepository>.Instance).AddAsync(account, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "AccessToken", "token_value", CancellationToken.None);
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).SetAsync(accountId, "RefreshToken", "refresh_value", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new CredentialRepository(_unitOfWork, _credentialEncryption, NullLogger<CredentialRepository>.Instance).DeleteByProviderAccountIdAsync(accountId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event createdEvent = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.Id = eventId;
                return await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).CreateAsync(evt, CancellationToken.None);
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
            string providerEventId = $"evt_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event upsertedEvent = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                return await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).UpsertAsync(evt, CancellationToken.None);
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
            string providerEventId = $"evt_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                evt.EventType = "Motion";
                _ = await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event updated = await _unitOfWork.ExecuteAsync(async context =>
            {
                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                evt.EventType = "Person";
                return await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).UpsertAsync(evt, CancellationToken.None);
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
            string providerEventId = $"evt_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                _ = await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(providerEventId, result.ProviderEventId);
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_GetByApiSourceHashAsync_ReturnsEvent()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            string apiHash = $"apihash_{Guid.NewGuid()}";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.ApiSourceHash = apiHash;
                _ = await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            Event? result = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).GetByApiSourceHashAsync(apiHash, CancellationToken.None);
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
            DateTime failureTime = DateTime.UtcNow;

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                Event evt = TestDataBuilder.BuildEvent(deviceId);
                evt.Id = eventId;
                _ = await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).CreateAsync(evt, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                await new EventRepository(_unitOfWork, NullLogger<EventRepository>.Instance).UpdateDownloadFailureAsync(eventId, failureTime, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                DeviceConfigSnapshot snapshot = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snapshot.Id = snapshotId;
                return await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snapshot, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DateTime now = DateTime.UtcNow;
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snap1.CapturedAtUtc = now.AddHours(-2);
                _ = await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snap1, CancellationToken.None);

                DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                snap2.CapturedAtUtc = now;
                _ = await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snap2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            DeviceConfigSnapshot? latest = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).GetLatestAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotNull(latest);
            Assert.True((now - latest.CapturedAtUtc).TotalSeconds < 1);
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_GetHistoryAsync_ReturnsAllSnapshots()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                _ = await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snap1, CancellationToken.None);

                DeviceConfigSnapshot snap2 = TestDataBuilder.BuildDeviceConfigSnapshot(deviceId);
                _ = await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snap2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> history = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).GetHistoryAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task UnitOfWorkDeviceConfigRepository_ListAsync_ReturnsAllSnapshots()
        {
            var loc1Id = Guid.NewGuid();
            var dev1Id = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = loc1Id;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(loc1Id);
                device.Id = dev1Id;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                DeviceConfigSnapshot snap1 = TestDataBuilder.BuildDeviceConfigSnapshot(dev1Id);
                _ = await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).AppendSnapshotAsync(snap1, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<DeviceConfigSnapshot> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new DeviceConfigRepository(_unitOfWork, NullLogger<DeviceConfigRepository>.Instance).ListAsync(CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                ProviderReconciliationRecord record = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                record.Id = recordId;
                return await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).AppendAsync(record, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderReconciliationRecord record1 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                ProviderReconciliationRecord record2 = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                _ = await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).AppendAsync(record1, CancellationToken.None);
                _ = await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).AppendAsync(record2, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderReconciliationRecord> history = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).GetHistoryForDeviceAsync(deviceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task UnitOfWorkProviderReconciliationRepository_ListAsync_ReturnsAllRecords()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ProviderReconciliationRecord record = TestDataBuilder.BuildProviderReconciliationRecord(deviceId);
                _ = await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).AppendAsync(record, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ProviderReconciliationRecord> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ProviderReconciliationRepository(_unitOfWork, NullLogger<ProviderReconciliationRepository>.Instance).ListAsync(CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                record.Id = recordId;

                ExportRecordItem item = TestDataBuilder.BuildExportRecordItem(recordId, mediaId);
                item.Id = itemId;

                return await new ExportRecordRepository(_unitOfWork, NullLogger<ExportRecordRepository>.Instance).AppendAsync(record, [item], CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Location location = TestDataBuilder.BuildLocation();
                location.Id = locationId;
                await new LocationRepository(_unitOfWork, NullLogger<LocationRepository>.Instance).AddAsync(location, CancellationToken.None);

                Device device = TestDataBuilder.BuildDevice(locationId);
                device.Id = deviceId;
                await new DeviceRepository(_unitOfWork, NullLogger<DeviceRepository>.Instance).AddAsync(device, CancellationToken.None);

                MediaItem media = TestDataBuilder.BuildMediaItem(deviceId);
                media.Id = mediaId;
                await new MediaItemRepository(_unitOfWork, NullLogger<MediaItemRepository>.Instance).AddAsync(media, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                record.Id = recordId;

                ExportRecordItem item = TestDataBuilder.BuildExportRecordItem(recordId, mediaId);
                _ = await new ExportRecordRepository(_unitOfWork, NullLogger<ExportRecordRepository>.Instance).AppendAsync(record, [item], CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportRecordItem> items = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportRecordRepository(_unitOfWork, NullLogger<ExportRecordRepository>.Instance).GetItemsForRecordAsync(recordId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(items);
            Assert.True(items.Any(i => i.MediaItemId == mediaId));
        }

        [Fact]
        public async Task UnitOfWorkExportRecordRepository_ListAsync_ReturnsAllRecords()
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                ExportRecord record = TestDataBuilder.BuildExportRecord();
                _ = await new ExportRecordRepository(_unitOfWork, NullLogger<ExportRecordRepository>.Instance).AppendAsync(record, [], CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportRecord> results = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportRecordRepository(_unitOfWork, NullLogger<ExportRecordRepository>.Instance).ListAsync(CancellationToken.None);
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
            string userId = $"user_{Guid.NewGuid()}";

            AccessAuditLogEntity recorded = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    evidenceId, "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    evidenceId, "user2", "Download", "192.168.1.2", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).GetForEvidenceAsync(evidenceId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, entries.Count);
            Assert.True(entries.Any(e => e.UserId == "user1" && e.Action == "View"));
            Assert.True(entries.Any(e => e.UserId == "user2" && e.Action == "Download"));
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_GetByUserAsync_ReturnsUserEntries()
        {
            string userId = "testuser";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    Guid.NewGuid(), userId, "View", "192.168.1.1", "Investigation", CancellationToken.None);
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    Guid.NewGuid(), userId, "Download", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).GetByUserAsync(userId, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    Guid.NewGuid(), "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).GetByDateRangeAsync(fromUtc, toUtc, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(entries);
            Assert.True(entries.All(e => e.AccessedAtUtc >= fromUtc && e.AccessedAtUtc <= toUtc));
        }

        [Fact]
        public async Task UnitOfWorkAccessAuditLogRepository_ListAsync_ReturnsPaginatedResults()
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).RecordAccessAsync(
                    Guid.NewGuid(), "user1", "View", "192.168.1.1", "Investigation", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<AccessAuditLogEntity> entries = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new AccessAuditLogRepository(_unitOfWork, NullLogger<AccessAuditLogRepository>.Instance).ListAsync(0, 10, CancellationToken.None);
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
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    locationId, "user1", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    locationId, "user2", 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).GetForLocationAsync(locationId, CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, records.Count);
            Assert.True(records.All(r => r.LocationId == locationId));
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetByUserAsync_ReturnsUserRecords()
        {
            string exportedBy = "TestUser";

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), exportedBy, 10, "AES256Archive", "CaseFile", CancellationToken.None);
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), exportedBy, 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).GetByUserAsync(exportedBy, CancellationToken.None);
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

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), "TestUser", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).GetByDateRangeAsync(fromUtc, toUtc, CancellationToken.None);
            }, CancellationToken.None);

            Assert.NotEmpty(records);
            Assert.True(records.All(r => r.ExportedAtUtc >= fromUtc && r.ExportedAtUtc <= toUtc));
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_GetStatisticsAsync_CalculatesStats()
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), "user1", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), "user2", 5, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            ExportStatistics stats = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).GetStatisticsAsync(CancellationToken.None);
            }, CancellationToken.None);

            Assert.Equal(2, stats.TotalExports);
            Assert.Equal(15, stats.TotalEventsExported);
            Assert.Equal(2, stats.UniqueExporters);
        }

        [Fact]
        public async Task UnitOfWorkExportAuditRecordRepository_ListAsync_ReturnsPaginatedResults()
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                _ = await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).RecordExportAsync(
                    Guid.NewGuid(), "TestUser", 10, "AES256Archive", "CaseFile", CancellationToken.None);
                return null;
            }, CancellationToken.None);

            IReadOnlyList<ExportAuditRecordEntity> records = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await new ExportAuditRecordRepository(_unitOfWork, NullLogger<ExportAuditRecordRepository>.Instance).ListAsync(0, 10, CancellationToken.None);
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
            return serviceType == typeof(ICredentialEncryptionProvider)
                ? _fixture.EncryptionProvider
                : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>)
                ? _loggerFactory.CreateLogger<ICredentialRepository>()
                : serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>) ? _loggerFactory.CreateLogger<UnitOfWork>() : (object?)null;
        }
    }
}
