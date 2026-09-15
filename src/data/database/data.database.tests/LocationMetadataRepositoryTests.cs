using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class LocationMetadataRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private LocationMetadataRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new LocationMetadataRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationMetadataRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task LocationMetadataRepository_AddAndGet_RoundTrips()
        {
            LocationMetadata metadata = BuildLocationMetadata();

            await _repository.AddAsync(metadata, CancellationToken.None);
            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(metadata.Id, retrieved.Id);
            Assert.Equal(metadata.LocationId, retrieved.LocationId);
            Assert.Equal("123 Test St", retrieved.StreetAddress);
            Assert.Equal("Test City", retrieved.City);
            Assert.Equal("TC", retrieved.State);
        }

        [Fact]
        public async Task LocationMetadataRepository_GetByLocationId_FindsMetadata()
        {
            Guid locationId = Guid.NewGuid();
            LocationMetadata metadata = BuildLocationMetadata(locationId: locationId);

            await _repository.AddAsync(metadata, CancellationToken.None);
            LocationMetadata? retrieved = await _repository.GetByLocationIdAsync(locationId, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(metadata.Id, retrieved.Id);
            Assert.Equal(locationId, retrieved.LocationId);
        }

        [Fact]
        public async Task LocationMetadataRepository_GetByApiHash_FindsMetadata()
        {
            string apiHash = "hash_abc123";
            LocationMetadata metadata = BuildLocationMetadata(apiResponseHash: apiHash);

            await _repository.AddAsync(metadata, CancellationToken.None);
            LocationMetadata? retrieved = await _repository.GetByApiHashAsync(apiHash, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(metadata.Id, retrieved.Id);
            Assert.Equal(apiHash, retrieved.ApiResponseHash);
        }

        [Fact]
        public async Task LocationMetadataRepository_GetAsync_ReturnsNullForMissing()
        {
            Guid nonexistentId = Guid.NewGuid();

            LocationMetadata? retrieved = await _repository.GetAsync(nonexistentId, CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationMetadataRepository_GetByLocationIdAsync_ReturnsNullForMissing()
        {
            Guid nonexistentLocationId = Guid.NewGuid();

            LocationMetadata? retrieved = await _repository.GetByLocationIdAsync(nonexistentLocationId, CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationMetadataRepository_GetByApiHashAsync_ReturnsNullForMissing()
        {
            string nonexistentHash = "nonexistent_hash";

            LocationMetadata? retrieved = await _repository.GetByApiHashAsync(nonexistentHash, CancellationToken.None);

            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationMetadataRepository_UpdateAsync_ModifiesData()
        {
            LocationMetadata metadata = BuildLocationMetadata();
            await _repository.AddAsync(metadata, CancellationToken.None);

            metadata.City = "Updated City";
            metadata.State = "UC";
            metadata.PostalCode = "99999";
            await _repository.UpdateAsync(metadata, CancellationToken.None);

            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated City", retrieved.City);
            Assert.Equal("UC", retrieved.State);
            Assert.Equal("99999", retrieved.PostalCode);
        }

        [Fact]
        public async Task LocationMetadataRepository_DeleteAsync_RemovesMetadata()
        {
            LocationMetadata metadata = BuildLocationMetadata();
            await _repository.AddAsync(metadata, CancellationToken.None);

            await _repository.DeleteAsync(metadata.Id, CancellationToken.None);

            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationMetadataRepository_DeleteAsync_WithNonexistentId_DoesNotThrow()
        {
            Guid nonexistentId = Guid.NewGuid();

            // Should not throw
            await _repository.DeleteAsync(nonexistentId, CancellationToken.None);
        }

        [Fact]
        public async Task LocationMetadataRepository_AddAsync_WithCompleteData_StoresAllFields()
        {
            DateTime now = DateTime.UtcNow;
            LocationMetadata metadata = new()
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                StreetAddress = "456 Complex Ave",
                City = "Complex City",
                State = "CC",
                PostalCode = "12345",
                Country = "USA",
                Latitude = 40.7128,
                Longitude = -74.0060,
                TimeZoneId = "America/New_York",
                IsOwner = true,
                LastSyncedUtc = now,
                SyncStatus = SyncStatus.Synced,
                ApiResponseHash = "hash_complex_xyz",
                MetadataJson = "{\"custom\": \"data\"}"
            };

            await _repository.AddAsync(metadata, CancellationToken.None);
            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal("456 Complex Ave", retrieved.StreetAddress);
            Assert.Equal("Complex City", retrieved.City);
            Assert.Equal("CC", retrieved.State);
            Assert.Equal("12345", retrieved.PostalCode);
            Assert.Equal("USA", retrieved.Country);
            Assert.Equal(40.7128, retrieved.Latitude);
            Assert.Equal(-74.0060, retrieved.Longitude);
            Assert.Equal("America/New_York", retrieved.TimeZoneId);
            Assert.True(retrieved.IsOwner);
            Assert.Equal(now, retrieved.LastSyncedUtc);
            Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
            Assert.Equal("hash_complex_xyz", retrieved.ApiResponseHash);
            Assert.Equal("{\"custom\": \"data\"}", retrieved.MetadataJson);
        }

        [Fact]
        public async Task LocationMetadataRepository_AddAsync_WithNullOptionalFields_Succeeds()
        {
            LocationMetadata metadata = new()
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                StreetAddress = null,
                City = null,
                State = null,
                PostalCode = null,
                Country = null,
                Latitude = null,
                Longitude = null,
                TimeZoneId = null,
                IsOwner = null,
                LastSyncedUtc = null,
                SyncStatus = SyncStatus.Pending,
                ApiResponseHash = null,
                MetadataJson = null
            };

            await _repository.AddAsync(metadata, CancellationToken.None);
            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Null(retrieved.StreetAddress);
            Assert.Null(retrieved.City);
            Assert.Null(retrieved.State);
            Assert.Equal(SyncStatus.Pending, retrieved.SyncStatus);
        }

        [Fact]
        public async Task LocationMetadataRepository_UpdateAsync_PartialUpdate_PreservesOtherFields()
        {
            LocationMetadata metadata = BuildLocationMetadata(
                city: "Original City",
                state: "OC",
                postalCode: "11111"
            );
            await _repository.AddAsync(metadata, CancellationToken.None);

            // Update only one field
            metadata.City = "New City";
            await _repository.UpdateAsync(metadata, CancellationToken.None);

            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("New City", retrieved.City);
            Assert.Equal("OC", retrieved.State);  // Should remain unchanged
            Assert.Equal("11111", retrieved.PostalCode);  // Should remain unchanged
        }

        [Fact]
        public async Task LocationMetadataRepository_MultipleMetadataPerLocation_LatestOverwrites()
        {
            Guid locationId = Guid.NewGuid();
            string apiHash1 = "hash_v1";
            string apiHash2 = "hash_v2";

            LocationMetadata metadata1 = BuildLocationMetadata(
                locationId: locationId,
                city: "City V1",
                apiResponseHash: apiHash1
            );

            await _repository.AddAsync(metadata1, CancellationToken.None);

            // Update with new data (latest overwrites)
            metadata1.City = "City V2";
            metadata1.StreetAddress = "New Address";
            metadata1.State = "ST";
            metadata1.PostalCode = "22222";
            metadata1.Country = "USA";
            metadata1.Latitude = 35.0;
            metadata1.Longitude = -100.0;
            metadata1.TimeZoneId = "America/Chicago";
            metadata1.IsOwner = false;
            metadata1.LastSyncedUtc = DateTime.UtcNow;
            metadata1.SyncStatus = SyncStatus.Synced;
            metadata1.ApiResponseHash = apiHash2;

            await _repository.UpdateAsync(metadata1, CancellationToken.None);

            // GetByLocationId should return the updated location metadata
            LocationMetadata? retrieved = await _repository.GetByLocationIdAsync(locationId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("City V2", retrieved.City);
            Assert.Equal(apiHash2, retrieved.ApiResponseHash);

            // Old hash should not be found (since it was updated)
            LocationMetadata? oldHash = await _repository.GetByApiHashAsync(apiHash1, CancellationToken.None);
            Assert.Null(oldHash);

            // New hash should be found
            LocationMetadata? newHash = await _repository.GetByApiHashAsync(apiHash2, CancellationToken.None);
            Assert.NotNull(newHash);
        }

        [Fact]
        public async Task LocationMetadataRepository_UpdateSyncStatus_Succeeds()
        {
            LocationMetadata metadata = BuildLocationMetadata(syncStatus: SyncStatus.Pending);
            await _repository.AddAsync(metadata, CancellationToken.None);

            metadata.SyncStatus = SyncStatus.Synced;
            metadata.LastSyncedUtc = DateTime.UtcNow;
            await _repository.UpdateAsync(metadata, CancellationToken.None);

            LocationMetadata? retrieved = await _repository.GetAsync(metadata.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
            Assert.NotNull(retrieved.LastSyncedUtc);
        }

        private static LocationMetadata BuildLocationMetadata(
            Guid? locationId = null,
            string? streetAddress = null,
            string? city = null,
            string? state = null,
            string? postalCode = null,
            string? country = null,
            double? latitude = null,
            double? longitude = null,
            string? timeZoneId = null,
            bool? isOwner = null,
            DateTime? lastSyncedUtc = null,
            SyncStatus syncStatus = SyncStatus.Pending,
            string? apiResponseHash = null,
            string? metadataJson = null)
        {
            return new LocationMetadata
            {
                Id = Guid.NewGuid(),
                LocationId = locationId ?? Guid.NewGuid(),
                StreetAddress = streetAddress ?? "123 Test St",
                City = city ?? "Test City",
                State = state ?? "TC",
                PostalCode = postalCode ?? "12345",
                Country = country ?? "USA",
                Latitude = latitude ?? 40.7128,
                Longitude = longitude ?? -74.0060,
                TimeZoneId = timeZoneId ?? "America/New_York",
                IsOwner = isOwner ?? true,
                LastSyncedUtc = lastSyncedUtc,
                SyncStatus = syncStatus,
                ApiResponseHash = apiResponseHash ?? $"hash_{Guid.NewGuid():N}",
                MetadataJson = metadataJson
            };
        }
    }
}
