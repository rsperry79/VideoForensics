using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class MediaItemPreviewQueryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private MediaItemRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new MediaItemRepository(_fixture.Factory, NullLogger<MediaItemRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        /// <summary>
        /// The preview backfill relies on this query to select only the rows it must act on: non-purged videos with no preview.
        /// A clip that already has a preview and a purged clip without one must both be excluded.
        /// </summary>
        [Fact]
        public async Task ListVideosWithoutPreviewAsync_ReturnsOnlyUnpurgedVideosWithoutPreview()
        {
            MediaItem wanted = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "a.mp4", "hash-wanted");
            wanted.MediaFormat = "video/mp4";

            MediaItem alreadyHasPreview = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "b.mp4", "hash-preview");
            alreadyHasPreview.MediaFormat = "video/mp4";
            alreadyHasPreview.PreviewPath = "b.preview.mp4";

            MediaItem purged = TestDataBuilder.BuildMediaItem(Guid.NewGuid(), null, "c.mp4", "hash-purged");
            purged.MediaFormat = "video/mp4";
            purged.IsPurged = true;

            foreach (MediaItem item in new[] { wanted, alreadyHasPreview, purged })
            {
                await _repository.AddAsync(item, CancellationToken.None);
            }

            IReadOnlyList<MediaItem> result = await _repository.ListVideosWithoutPreviewAsync(10, CancellationToken.None);

            Assert.Equal(new[] { wanted.Id }, result.Select(m => m.Id).ToArray());
        }
    }
}
