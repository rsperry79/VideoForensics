using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Database.Repositories;

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
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
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
            var accountId = Guid.NewGuid();
            var location = TestDataBuilder.BuildLocation(accountId, "loc_123", "Front Door");

            await _repository.AddAsync(location, CancellationToken.None);
            var retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(location.Id, retrieved.Id);
            Assert.Equal(accountId, retrieved.ProviderAccountId);
            Assert.Equal("loc_123", retrieved.ProviderLocationId);
            Assert.Equal("Front Door", retrieved.Name);
        }

        [Fact]
        public async Task LocationRepository_GetByProviderLocationId_FindsLocation()
        {
            var accountId = Guid.NewGuid();
            var location = TestDataBuilder.BuildLocation(accountId, "loc_456");

            await _repository.AddAsync(location, CancellationToken.None);
            var retrieved = await _repository.GetByProviderLocationIdAsync(accountId, "loc_456", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(location.Id, retrieved.Id);
        }

        [Fact]
        public async Task LocationRepository_GetByProviderAccountId_ReturnsAllForAccount()
        {
            var accountId = Guid.NewGuid();
            var otherAccountId = Guid.NewGuid();

            var loc1 = TestDataBuilder.BuildLocation(accountId);
            var loc2 = TestDataBuilder.BuildLocation(accountId);
            var loc3 = TestDataBuilder.BuildLocation(otherAccountId);

            await _repository.AddAsync(loc1, CancellationToken.None);
            await _repository.AddAsync(loc2, CancellationToken.None);
            await _repository.AddAsync(loc3, CancellationToken.None);

            var list = await _repository.GetByProviderAccountIdAsync(accountId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task LocationRepository_UpdateAsync_ModifiesData()
        {
            var location = TestDataBuilder.BuildLocation();
            await _repository.AddAsync(location, CancellationToken.None);

            location.Name = "Updated Location";
            await _repository.UpdateAsync(location, CancellationToken.None);

            var retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Location", retrieved.Name);
        }

        [Fact]
        public async Task LocationRepository_DeleteAsync_RemovesLocation()
        {
            var location = TestDataBuilder.BuildLocation();
            await _repository.AddAsync(location, CancellationToken.None);

            await _repository.DeleteAsync(location.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(location.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task LocationRepository_ListAsync_ReturnsAll()
        {
            var loc1 = TestDataBuilder.BuildLocation();
            var loc2 = TestDataBuilder.BuildLocation();

            await _repository.AddAsync(loc1, CancellationToken.None);
            await _repository.AddAsync(loc2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task LocationRepository_UniqueConstraint_DuplicateAccountLocationComboThrows()
        {
            var accountId = Guid.NewGuid();
            var loc1 = TestDataBuilder.BuildLocation(accountId, "dup_loc");
            var loc2 = TestDataBuilder.BuildLocation(accountId, "dup_loc");

            await _repository.AddAsync(loc1, CancellationToken.None);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await _repository.AddAsync(loc2, CancellationToken.None));
        }
    }
}
