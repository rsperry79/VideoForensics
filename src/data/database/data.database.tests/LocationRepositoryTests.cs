using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class LocationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private LocationRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task LocationRepository_AddAndGet_RoundTrips()
        {
            Location location = TestDataBuilder.BuildLocation("loc_123", "Front Door");

            await _repository.AddAsync(location, CancellationToken.None);
            Location? retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(location.Id, retrieved.Id);
            Assert.Equal("loc_123", retrieved.ProviderLocationId);
            Assert.Equal("Front Door", retrieved.Name);
        }

        [Fact]
        public async Task LocationRepository_GetByProviderLocationId_FindsLocation()
        {
            Location location = TestDataBuilder.BuildLocation("loc_456");

            await _repository.AddAsync(location, CancellationToken.None);
            Location? retrieved = await _repository.GetByProviderLocationIdAsync("loc_456", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(location.Id, retrieved.Id);
        }

        [Fact]
        public async Task LocationRepository_GetByProviderAccountId_ReturnsAllLocations()
        {
            Location loc1 = TestDataBuilder.BuildLocation();
            Location loc2 = TestDataBuilder.BuildLocation();
            Location loc3 = TestDataBuilder.BuildLocation();

            await _repository.AddAsync(loc1, CancellationToken.None);
            await _repository.AddAsync(loc2, CancellationToken.None);
            await _repository.AddAsync(loc3, CancellationToken.None);

            IReadOnlyList<Location> list = await _repository.GetByProviderAccountIdAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Equal(3, list.Count);
        }

        [Fact]
        public async Task LocationRepository_UpdateAsync_ModifiesData()
        {
            Location location = TestDataBuilder.BuildLocation();
            await _repository.AddAsync(location, CancellationToken.None);

            location.Name = "Updated Location";
            await _repository.UpdateAsync(location, CancellationToken.None);

            Location? retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Location", retrieved.Name);
        }

        [Fact]
        public async Task LocationRepository_DeleteAsync_RemovesLocation()
        {
            Location location = TestDataBuilder.BuildLocation();
            await _repository.AddAsync(location, CancellationToken.None);

            await _repository.DeleteAsync(location.Id, CancellationToken.None);

            Location? retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationRepository_ListAsync_ReturnsAll()
        {
            Location loc1 = TestDataBuilder.BuildLocation();
            Location loc2 = TestDataBuilder.BuildLocation();

            await _repository.AddAsync(loc1, CancellationToken.None);
            await _repository.AddAsync(loc2, CancellationToken.None);

            IReadOnlyList<Location> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task LocationRepository_UniqueConstraint_DuplicateAccountLocationComboThrows()
        {
            Location loc1 = TestDataBuilder.BuildLocation("dup_loc");
            Location loc2 = TestDataBuilder.BuildLocation("dup_loc");

            await _repository.AddAsync(loc1, CancellationToken.None);

            _ = await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(loc2, CancellationToken.None));
        }
    }
}
