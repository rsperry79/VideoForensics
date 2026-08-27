using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using Microsoft.EntityFrameworkCore;

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

            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
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

            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                var account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var retrievedUser = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var retrievedAccount = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);

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
                await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    var user = TestDataBuilder.BuildUser();
                    user.Id = userId;
                    await context.Users.AddAsync(user, CancellationToken.None);

                    var account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                    throw new InvalidOperationException("Simulated failure");
                }, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            var ctx = _fixture.Factory.CreateDbContext();
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
                var user = TestDataBuilder.BuildUser();
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

            var user1 = await _unitOfWork.ExecuteAsync(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId1;
                await context.Users.AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            var user2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId2;
                await context.Users.AddAsync(user, CancellationToken.None);
                return user;
            }, CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var count = await ctx.Users.CountAsync();
            Assert.Equal(2, count);
        }


        [Fact]
        public async Task UnitOfWork_ActionLogChaining_AcrossMultipleExecutes()
        {
            var entry1 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ActionLog.AppendAsync(
                    "A1", ActorType.Human, "Act1", "Ent", null, null, CancellationToken.None);
            }, CancellationToken.None);

            var entry2 = await _unitOfWork.ExecuteAsync(async context =>
            {
                return await context.ActionLog.AppendAsync(
                    "A2", ActorType.Human, "Act2", "Ent", null, null, CancellationToken.None);
            }, CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var retrieved1 = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == entry1.Id);
            var retrieved2 = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == entry2.Id);

            Assert.NotNull(retrieved2.PreviousEntryHash);
            Assert.Equal(retrieved1.EntryHash, retrieved2.PreviousEntryHash);
        }

        [Fact]
        public async Task UnitOfWork_ComplexScenario_MultipleEntitiesAndLog()
        {
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();

            var logEntry = await _unitOfWork.ExecuteAsync(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                var account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                var location = TestDataBuilder.BuildLocation(accountId);
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                var logEntry = await context.ActionLog.AppendAsync(
                    "TestUser", ActorType.Human, "UserAndLocationCreated", "Location", locationId, null, CancellationToken.None);

                return logEntry;
            }, CancellationToken.None);

            var ctx = _fixture.Factory.CreateDbContext();
            var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var account = await ctx.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == accountId);
            var location = await ctx.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            var log = await ctx.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == logEntry.Id);

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
                await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    var user = TestDataBuilder.BuildUser();
                    user.Id = userId;
                    await context.Users.AddAsync(user, CancellationToken.None);

                    var account = TestDataBuilder.BuildProviderAccount(userId);
                    account.Id = accountId;
                    await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                    throw new DbUpdateException("Simulated constraint violation");
                }, CancellationToken.None);
            }
            catch (DbUpdateException)
            {
            }

            var ctx = _fixture.Factory.CreateDbContext();
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
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                var account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureLocation call - should create
            Location? location1 = null;
            await _unitOfWork.ExecuteAsync(async context =>
            {
                var locations = await context.Locations.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
                var existing = locations.FirstOrDefault(l => l.ProviderLocationId == providerLocationId);

                if (existing == null)
                {
                    var location = new Location
                    {
                        Id = Guid.NewGuid(),
                        ProviderAccountId = accountId,
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
            await _unitOfWork.ExecuteAsync(async context =>
            {
                var locations = await context.Locations.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
                var existing = locations.FirstOrDefault(l => l.ProviderLocationId == providerLocationId);

                if (existing == null)
                {
                    var location = new Location
                    {
                        Id = Guid.NewGuid(),
                        ProviderAccountId = accountId,
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
            var ctx = _fixture.Factory.CreateDbContext();
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
            await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                var user = TestDataBuilder.BuildUser();
                user.Id = userId;
                await context.Users.AddAsync(user, CancellationToken.None);

                var account = TestDataBuilder.BuildProviderAccount(userId);
                account.Id = accountId;
                await context.ProviderAccounts.AddAsync(account, CancellationToken.None);

                var location = TestDataBuilder.BuildLocation(accountId);
                location.Id = locationId;
                await context.Locations.AddAsync(location, CancellationToken.None);

                return null;
            }, CancellationToken.None);

            // First EnsureDevice call - should create
            Device? device1 = null;
            await _unitOfWork.ExecuteAsync(async context =>
            {
                var devices = await context.Devices.GetByLocationIdAsync(locationId, CancellationToken.None);
                var existing = devices.FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);

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
            await _unitOfWork.ExecuteAsync(async context =>
            {
                var devices = await context.Devices.GetByLocationIdAsync(locationId, CancellationToken.None);
                var existing = devices.FirstOrDefault(d => d.ProviderDeviceId == providerDeviceId);

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
            var ctx = _fixture.Factory.CreateDbContext();
            var retrievedDevice = await ctx.Devices.FirstOrDefaultAsync(d => d.ProviderDeviceId == providerDeviceId);
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
                    return _fixture.EncryptionProvider;
                if (serviceType == typeof(Microsoft.Extensions.Logging.ILogger<ICredentialRepository>))
                    return _loggerFactory.CreateLogger<ICredentialRepository>();
                if (serviceType == typeof(Microsoft.Extensions.Logging.ILogger<UnitOfWork>))
                    return _loggerFactory.CreateLogger<UnitOfWork>();
                return null;
            }
        }
    }
}
