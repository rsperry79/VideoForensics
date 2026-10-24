using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class MediaItemRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private MediaItemRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
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
            MediaItem item = TestDataBuilder.BuildMediaItem(deviceId, null, "video.mp4", "abc123def456");

            await _repository.AddAsync(item, CancellationToken.None);
            MediaItem? retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(item.Id, retrieved.Id);
            Assert.Equal(deviceId, retrieved.DeviceId);
            Assert.Equal("video.mp4", retrieved.FileName);
            Assert.Equal("abc123def456", retrieved.Sha256Hash);
        }

        /// <summary>
        /// The backfill relies on this query to select only the rows it must act on: unthumbnailed, non-purged videos.
        /// Images, rows that already have a thumbnail, and purged clips must all be excluded.
        /// </summary>
        [Fact]
        public async Task ListVideosWithoutThumbnailAsync_ReturnsOnlyUnthumbnailedVideos()
        {
            MediaItem wanted = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "a.mp4", "hash-wanted");
            wanted.MediaFormat = "video/mp4";

            MediaItem alreadyHasThumbnail = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "b.mp4", "hash-thumb");
            alreadyHasThumbnail.MediaFormat = "video/mp4";
            alreadyHasThumbnail.ThumbnailPath = "b.thumb.png";

            MediaItem still = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "c.jpg", "hash-still");
            still.MediaFormat = "image/jpeg";

            MediaItem purged = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "d.mp4", "hash-purged");
            purged.MediaFormat = "video/mp4";
            purged.IsPurged = true;

            foreach (MediaItem item in new[] { wanted, alreadyHasThumbnail, still, purged })
            {
                await _repository.AddAsync(item, CancellationToken.None);
            }

            IReadOnlyList<MediaItem> result = await _repository.ListVideosWithoutThumbnailAsync(CancellationToken.None);

            Assert.Equal(new[] { wanted.Id }, result.Select(m => m.Id).ToArray());
        }

        [Fact]
        public async Task MediaItemRepository_GetByHash_FindsItem()
        {
            MediaItem item = TestDataBuilder.BuildMediaItem(null, null, null, "unique_hash_123");

            await _repository.AddAsync(item, CancellationToken.None);
            MediaItem? retrieved = await _repository.GetByHashAsync("unique_hash_123", CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(item.Id, retrieved.Id);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDeviceId_ReturnsAllForDevice()
        {
            var deviceId = Guid.NewGuid();
            var otherDeviceId = Guid.NewGuid();

            MediaItem item1 = TestDataBuilder.BuildMediaItem(deviceId);
            MediaItem item2 = TestDataBuilder.BuildMediaItem(deviceId);
            MediaItem item3 = TestDataBuilder.BuildMediaItem(otherDeviceId);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            IReadOnlyList<MediaItem> list = await _repository.GetByDeviceIdAsync(deviceId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDownloadEventId_ReturnsItemsForEvent()
        {
            var downloadEventId = Guid.NewGuid();
            var otherEventId = Guid.NewGuid();

            MediaItem item1 = TestDataBuilder.BuildMediaItem(null, downloadEventId);
            MediaItem item2 = TestDataBuilder.BuildMediaItem(null, downloadEventId);
            MediaItem item3 = TestDataBuilder.BuildMediaItem(null, otherEventId);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            IReadOnlyList<MediaItem> list = await _repository.GetByDownloadEventIdAsync(downloadEventId, CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task MediaItemRepository_GetByDeviceAndDateRange_FiltersCorrectly()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            MediaItem item1 = TestDataBuilder.BuildMediaItem(deviceId);
            item1.RecordedAtUtc = now.AddHours(-2);

            MediaItem item2 = TestDataBuilder.BuildMediaItem(deviceId);
            item2.RecordedAtUtc = now;

            MediaItem item3 = TestDataBuilder.BuildMediaItem(deviceId);
            item3.RecordedAtUtc = now.AddHours(2);

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);
            await _repository.AddAsync(item3, CancellationToken.None);

            IReadOnlyList<MediaItem> list = await _repository.GetByDeviceAndDateRangeAsync(
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
            MediaItem item = TestDataBuilder.BuildMediaItem();
            await _repository.AddAsync(item, CancellationToken.None);

            item.FileName = "updated.mp4";
            await _repository.UpdateAsync(item, CancellationToken.None);

            MediaItem? retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("updated.mp4", retrieved.FileName);
        }

        [Fact]
        public async Task MediaItemRepository_DeleteAsync_RemovesItem()
        {
            MediaItem item = TestDataBuilder.BuildMediaItem();
            await _repository.AddAsync(item, CancellationToken.None);

            await _repository.DeleteAsync(item.Id, CancellationToken.None);

            MediaItem? retrieved = await _repository.GetAsync(item.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task MediaItemRepository_ListAsync_ReturnsAll()
        {
            MediaItem item1 = TestDataBuilder.BuildMediaItem();
            MediaItem item2 = TestDataBuilder.BuildMediaItem();

            await _repository.AddAsync(item1, CancellationToken.None);
            await _repository.AddAsync(item2, CancellationToken.None);

            IReadOnlyList<MediaItem> list = await _repository.ListAsync(CancellationToken.None);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenFileNameContainsNewlines()
        {
            // Arrange: Create a media item with FileName containing CRLF (log injection attempt)
            string injectedFileName = "video.mp4\r\nFAKE LOG ENTRY: File decrypted successfully";
            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = injectedFileName,
                FilePath = "/path/to/video.mp4",
                MediaFormat = "video/mp4",
                FileSizeBytes = 1024000,
                Sha256Hash = "hash123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };

            // Act: Add the media item (logs it)
            await _repository.AddAsync(mediaItem, CancellationToken.None);

            // Assert: Verify the media item was persisted with the raw FileName intact
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            MediaItem? persistedItem = await db.MediaItems.FindAsync(new object[] { mediaItem.Id }, cancellationToken: CancellationToken.None);
            Assert.NotNull(persistedItem);
            // The raw FileName (with newlines) should be stored in the database
            Assert.Equal(injectedFileName, persistedItem.FileName);
        }
    }
}
