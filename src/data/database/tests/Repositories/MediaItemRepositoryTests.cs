using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class MediaItemRepositoryTests : RepositoryTestBase
    {
        private MediaItemRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new MediaItemRepository(Fixture.Factory, CreateLogger<MediaItemRepository>());
        }

        [Fact]
        public async Task AddAsync_SanitizesLogOutput_WhenFileNameContainsNewlines()
        {
            // Arrange: Create a media item with FileName containing CRLF (log injection attempt)
            var injectedFileName = "video.mp4\r\nFAKE LOG ENTRY: File decrypted successfully";
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
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                MediaItem? persistedItem = await db.MediaItems.FindAsync(new object[] { mediaItem.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persistedItem);
                // The raw FileName (with newlines) should be stored in the database
                Assert.Equal(injectedFileName, persistedItem.FileName);
            }
        }
    }
}
