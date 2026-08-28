using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Enforces: ALL API data must be persisted to database before use.</summary>
    public class DataPersistenceTests
    {
        private VideoForensicsDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new VideoForensicsDbContext(options);
        }

        [Fact]
        public async Task RingAccount_PersistsSubscriptionData()
        {
            using var db = CreateContext();

            var account = new RingAccount
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = Guid.NewGuid(),
                SubscriptionLevel = "premium",
                AccountEmail = "test@ring.com",
                AuthenticatedAtUtc = DateTime.UtcNow
            };

            db.RingAccounts.Add(account);
            await db.SaveChangesAsync();

            var retrieved = await db.RingAccounts.FirstOrDefaultAsync(a => a.Id == account.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("premium", retrieved.SubscriptionLevel);
            Assert.NotNull(retrieved.AuthenticatedAtUtc);
        }

        [Fact]
        public async Task Device_TracksCacheMetadata()
        {
            using var db = CreateContext();

            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbot",
                LastSyncedUtc = DateTime.UtcNow,
                SyncStatus = SyncStatus.Synced,
                ApiResponseHash = "abc123"
            };

            db.Devices.Add(device);
            await db.SaveChangesAsync();

            var retrieved = await db.Devices.FirstOrDefaultAsync(d => d.Id == device.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
            Assert.NotNull(retrieved.LastSyncedUtc);
            Assert.NotNull(retrieved.ApiResponseHash);
        }

        [Fact]
        public async Task Location_TracksSyncTimestamp()
        {
            using var db = CreateContext();

            var location = new Location
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = Guid.NewGuid(),
                ProviderLocationId = "loc-456",
                Name = "Home",
                LastSyncedUtc = DateTime.UtcNow,
                SyncStatus = SyncStatus.Synced
            };

            db.Locations.Add(location);
            await db.SaveChangesAsync();

            var retrieved = await db.Locations.FirstOrDefaultAsync(l => l.Id == location.Id);
            Assert.NotNull(retrieved.LastSyncedUtc);
            Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
        }

        [Fact]
        public async Task DeviceCapabilities_StoresSpecsFromAPI()
        {
            using var db = CreateContext();

            var caps = new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                Resolution = "1080p",
                HasAudio = true,
                HasNightVision = true,
                FirmwareVersion = "1.8.26"
            };

            db.DeviceCapabilities.Add(caps);
            await db.SaveChangesAsync();

            var retrieved = await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.Id == caps.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("1080p", retrieved.Resolution);
            Assert.True(retrieved.HasAudio);
        }

        [Fact]
        public async Task LocationMetadata_StoresAddressComponents()
        {
            using var db = CreateContext();

            var metadata = new LocationMetadata
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                StreetAddress = "123 Main St",
                City = "Springfield",
                State = "IL",
                PostalCode = "62701"
            };

            db.LocationMetadata.Add(metadata);
            await db.SaveChangesAsync();

            var retrieved = await db.LocationMetadata.FirstOrDefaultAsync(m => m.Id == metadata.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("123 Main St", retrieved.StreetAddress);
            Assert.Equal("Springfield", retrieved.City);
        }

        [Fact]
        public async Task DeviceHealth_PersistsCurrentStatus()
        {
            using var db = CreateContext();

            var health = new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                BatteryPercentage = 85m,
                WifiSignalRssi = -45,
                IsOnline = true,
                LastHeartbeatUtc = DateTime.UtcNow
            };

            db.DeviceHealthRecords.Add(health);
            await db.SaveChangesAsync();

            var retrieved = await db.DeviceHealthRecords.FirstOrDefaultAsync(h => h.Id == health.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(85m, retrieved.BatteryPercentage);
            Assert.True(retrieved.IsOnline);
        }
    }
}
