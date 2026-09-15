using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DeviceCapabilitiesRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceCapabilitiesRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceCapabilitiesRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceCapabilitiesRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_AddAsync_CreatesRecord()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);

            await _repository.AddAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(capabilities.Id, retrieved.Id);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("1920x1080", retrieved.Resolution);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetAsync_ReturnsNullWhenNotFound()
        {
            var nonExistentId = Guid.NewGuid();

            DeviceCapabilities? result = await _repository.GetAsync(nonExistentId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByDeviceIdAsync_FindsCapabilities()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);

            await _repository.AddAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetByDeviceIdAsync(deviceId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(capabilities.Id, retrieved.Id);
            Assert.Equal(deviceId, retrieved.DeviceId);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByDeviceIdAsync_ReturnsNullWhenDeviceNotFound()
        {
            var nonExistentDeviceId = Guid.NewGuid();

            DeviceCapabilities? result = await _repository.GetByDeviceIdAsync(nonExistentDeviceId, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByApiHashAsync_FindsCapabilities()
        {
            var deviceId = Guid.NewGuid();
            string apiHash = "abc123def456";
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId, apiHash: apiHash);

            await _repository.AddAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetByApiHashAsync(apiHash, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(capabilities.Id, retrieved.Id);
            Assert.Equal(apiHash, retrieved.ApiResponseHash);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByApiHashAsync_ReturnsNullWhenHashNotFound()
        {
            string nonExistentHash = "nonexistent_hash";

            DeviceCapabilities? result = await _repository.GetByApiHashAsync(nonExistentHash, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_UpdateAsync_ModifiesRecord()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);
            await _repository.AddAsync(capabilities, CancellationToken.None);

            capabilities.Resolution = "2560x1440";
            capabilities.HasNightVision = false;
            capabilities.MaxStorageDays = 60;
            await _repository.UpdateAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("2560x1440", retrieved.Resolution);
            Assert.False(retrieved.HasNightVision);
            Assert.Equal(60, retrieved.MaxStorageDays);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_UpdateAsync_UpdatesSyncStatus()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);
            capabilities.SyncStatus = SyncStatus.Pending;
            await _repository.AddAsync(capabilities, CancellationToken.None);

            capabilities.SyncStatus = SyncStatus.Stale;
            capabilities.LastSyncedUtc = DateTime.UtcNow;
            await _repository.UpdateAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(SyncStatus.Stale, retrieved.SyncStatus);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_DeleteAsync_RemovesRecord()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);
            await _repository.AddAsync(capabilities, CancellationToken.None);

            await _repository.DeleteAsync(capabilities.Id, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_DeleteAsync_WithNonExistentId_DoesNotThrow()
        {
            var nonExistentId = Guid.NewGuid();

            // Should not throw
            await _repository.DeleteAsync(nonExistentId, CancellationToken.None);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_AddAsync_PreservesAllProperties()
        {
            var deviceId = Guid.NewGuid();
            DateTime syncedAt = DateTime.UtcNow;

            var capabilities = new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Resolution = "4K",
                HasAudio = true,
                HasNightVision = true,
                HasMotionDetection = true,
                HasCloudStorage = false,
                StorageType = "Local",
                MaxStorageDays = 90,
                FirmwareVersion = "3.0.0",
                HardwareModel = "ProModel",
                LastSyncedUtc = syncedAt,
                SyncStatus = SyncStatus.Synced,
                ApiResponseHash = "hash123456",
                MetadataJson = "{\"extra\": \"data\"}"
            };

            await _repository.AddAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("4K", retrieved.Resolution);
            Assert.True(retrieved.HasAudio);
            Assert.True(retrieved.HasNightVision);
            Assert.True(retrieved.HasMotionDetection);
            Assert.False(retrieved.HasCloudStorage);
            Assert.Equal("Local", retrieved.StorageType);
            Assert.Equal(90, retrieved.MaxStorageDays);
            Assert.Equal("3.0.0", retrieved.FirmwareVersion);
            Assert.Equal("ProModel", retrieved.HardwareModel);
            Assert.Equal(syncedAt, retrieved.LastSyncedUtc);
            Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
            Assert.Equal("hash123456", retrieved.ApiResponseHash);
            Assert.Equal("{\"extra\": \"data\"}", retrieved.MetadataJson);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_AddAsync_WithNullValues_Succeeds()
        {
            var deviceId = Guid.NewGuid();

            var capabilities = new DeviceCapabilities
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Resolution = null,
                HasAudio = null,
                HasNightVision = null,
                HasMotionDetection = null,
                HasCloudStorage = null,
                StorageType = null,
                MaxStorageDays = null,
                FirmwareVersion = null,
                HardwareModel = null,
                LastSyncedUtc = null,
                SyncStatus = SyncStatus.Pending,
                ApiResponseHash = null,
                MetadataJson = null
            };

            await _repository.AddAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved.Resolution);
            Assert.Null(retrieved.HasAudio);
            Assert.Null(retrieved.LastSyncedUtc);
            Assert.Equal(SyncStatus.Pending, retrieved.SyncStatus);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByDeviceIdAsync_OnlyReturnsForSpecificDevice()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();

            DeviceCapabilities cap1 = TestDataBuilder.BuildDeviceCapabilities(deviceId1);
            DeviceCapabilities cap2 = TestDataBuilder.BuildDeviceCapabilities(deviceId2);

            await _repository.AddAsync(cap1, CancellationToken.None);
            await _repository.AddAsync(cap2, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetByDeviceIdAsync(deviceId1, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(deviceId1, retrieved.DeviceId);
            Assert.Equal(cap1.Id, retrieved.Id);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_GetByApiHashAsync_OnlyReturnsForSpecificHash()
        {
            string hash1 = "hash_001";
            string hash2 = "hash_002";

            DeviceCapabilities cap1 = TestDataBuilder.BuildDeviceCapabilities(apiHash: hash1);
            DeviceCapabilities cap2 = TestDataBuilder.BuildDeviceCapabilities(apiHash: hash2);

            await _repository.AddAsync(cap1, CancellationToken.None);
            await _repository.AddAsync(cap2, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetByApiHashAsync(hash1, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(hash1, retrieved.ApiResponseHash);
            Assert.Equal(cap1.Id, retrieved.Id);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_UpdateAsync_PreservesId()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);
            Guid originalId = capabilities.Id;
            await _repository.AddAsync(capabilities, CancellationToken.None);

            capabilities.Resolution = "Updated";
            await _repository.UpdateAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(originalId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(originalId, retrieved.Id);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_UpdateAsync_UpdatesLastSynced()
        {
            var deviceId = Guid.NewGuid();
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId);
            DateTime originalTime = DateTime.UtcNow.AddHours(-1);
            capabilities.LastSyncedUtc = originalTime;
            await _repository.AddAsync(capabilities, CancellationToken.None);

            DateTime newTime = DateTime.UtcNow;
            capabilities.LastSyncedUtc = newTime;
            await _repository.UpdateAsync(capabilities, CancellationToken.None);

            DeviceCapabilities? retrieved = await _repository.GetAsync(capabilities.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.True(Math.Abs((retrieved.LastSyncedUtc.Value - newTime).TotalSeconds) < 1);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_AddAsync_AllowsMultipleForDifferentDevices()
        {
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();

            DeviceCapabilities cap1 = TestDataBuilder.BuildDeviceCapabilities(deviceId1);
            DeviceCapabilities cap2 = TestDataBuilder.BuildDeviceCapabilities(deviceId2);

            // Should not throw constraint error
            await _repository.AddAsync(cap1, CancellationToken.None);
            await _repository.AddAsync(cap2, CancellationToken.None);

            DeviceCapabilities? retrieved1 = await _repository.GetByDeviceIdAsync(deviceId1, CancellationToken.None);
            DeviceCapabilities? retrieved2 = await _repository.GetByDeviceIdAsync(deviceId2, CancellationToken.None);

            Assert.NotNull(retrieved1);
            Assert.NotNull(retrieved2);
            Assert.NotEqual(retrieved1.Id, retrieved2.Id);
        }

        [Fact]
        public async Task DeviceCapabilitiesRepository_DeleteAsync_ClearsApiHash()
        {
            var deviceId = Guid.NewGuid();
            string apiHash = "unique_hash_001";
            DeviceCapabilities capabilities = TestDataBuilder.BuildDeviceCapabilities(deviceId, apiHash: apiHash);
            await _repository.AddAsync(capabilities, CancellationToken.None);

            await _repository.DeleteAsync(capabilities.Id, CancellationToken.None);

            // Should not find by hash anymore
            DeviceCapabilities? retrieved = await _repository.GetByApiHashAsync(apiHash, CancellationToken.None);
            Assert.Null(retrieved);
        }
    }
}
