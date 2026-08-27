using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class MediaItemRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private MediaItemRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new MediaItemRepository(_fixture.Factory, loggerFactory.CreateLogger<MediaItemRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task MediaItemRepository_AddAndGet_RoundTrips()
        {
            var deviceId = Guid.NewGuid();
            var item = TestDataBuilder.BuildMediaItem(deviceId, null, "video.mp4", "abc123def456");

            await _repository.AddAsync(item, CancellationToken.None);
            var retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(item.Id, retrieved.Id);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("video.mp4", retrieved.FileName);
            Assert.Equal("abc123def456", retrieved.Sha256Hash);
        }

        [Fact]
        public async Task MediaItemRepository_GetByHash_FindsItem()
        {
            var item = TestDataBuilder.BuildMediaItem(null, null, null, "unique_hash_123");

            await _repository.AddAsync(item, CancellationToken.None);
            var retrieved = await _repository.GetByHashAsync("unique_hash_123", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(item.Id, retrieved.Id);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDeviceId_ReturnsAllForDevice()
        {
            var deviceId = Guid.NewGuid();
            var otherDeviceId = Guid.NewGuid();

            var item1 = TestDataBuilder.BuildMediaItem(deviceId);
            var item2 = TestDataBuilder.BuildMediaItem(deviceId);
            var item3 = TestDataBuilder.BuildMediaItem(otherDeviceId);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            var list = await _repository.GetByDeviceIdAsync(deviceId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDownloadEventId_ReturnsItemsForEvent()
        {
            var downloadEventId = Guid.NewGuid();
            var otherEventId = Guid.NewGuid();

            var item1 = TestDataBuilder.BuildMediaItem(null, downloadEventId);
            var item2 = TestDataBuilder.BuildMediaItem(null, downloadEventId);
            var item3 = TestDataBuilder.BuildMediaItem(null, otherEventId);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            var list = await _repository.GetByDownloadEventIdAsync(downloadEventId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDeviceAndDateRange_FiltersCorrectly()
        {
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var item1 = TestDataBuilder.BuildMediaItem(deviceId);
            item1.RecordedAtUtc = now.AddHours(-2);

            var item2 = TestDataBuilder.BuildMediaItem(deviceId);
            item2.RecordedAtUtc = now;

            var item3 = TestDataBuilder.BuildMediaItem(deviceId);
            item3.RecordedAtUtc = now.AddHours(2);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            var list = await _repository.GetByDeviceAndDateRangeAsync(
                deviceId,
                now.AddHours(-1),
                now.AddHours(1),
                CancellationToken.None);

            Assert.Equal(1, list.Count);
            Assert.Equal(item2.Id, list[0].Id);
        }

        [Fact]
        public async Task MediaItemRepository_UpdateAsync_ModifiesData()
        {
            var item = TestDataBuilder.BuildMediaItem();
            await _repository.AddAsync(item, CancellationToken.None);

            item.FileName = "updated.mp4";
            await _repository.UpdateAsync(item, CancellationToken.None);

            var retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("updated.mp4", retrieved.FileName);
        }

        [Fact]
        public async Task MediaItemRepository_DeleteAsync_RemovesItem()
        {
            var item = TestDataBuilder.BuildMediaItem();
            await _repository.AddAsync(item, CancellationToken.None);

            await _repository.DeleteAsync(item.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task MediaItemRepository_ListAsync_ReturnsAll()
        {
            var item1 = TestDataBuilder.BuildMediaItem();
            var item2 = TestDataBuilder.BuildMediaItem();

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }
    }
}
