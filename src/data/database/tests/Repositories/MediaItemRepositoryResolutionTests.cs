using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>The observed-resolution fallback reads distinct video resolutions only, for one device.</summary>
    public class MediaItemRepositoryResolutionTests : IAsyncLifetime
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
        public async Task GetDistinctVideoResolutionsAsync_ReturnsVideoResolutionsOfThisDeviceOnly()
        {
            var deviceId = Guid.NewGuid();
            await AddAsync(deviceId, "video/mp4", "1920x1080", "a");
            await AddAsync(deviceId, "video/mp4", "1920x1080", "b");
            await AddAsync(deviceId, "video/mp4", "640x360", "c");
            await AddAsync(deviceId, "jpg", "3840x2160", "d");
            await AddAsync(Guid.NewGuid(), "video/mp4", "1280x720", "e");

            IReadOnlyList<string> resolutions = await _repository.GetDistinctVideoResolutionsAsync(deviceId, CancellationToken.None);

            Assert.Equal(new[] { "1920x1080", "640x360" }, resolutions.OrderBy(r => r, StringComparer.Ordinal));
        }

        private async Task AddAsync(Guid deviceId, string format, string resolution, string hash)
        {
            MediaItem item = TestDataBuilder.BuildMediaItem(deviceId, null, hash + ".mp4", "hash-" + hash);
            item.MediaFormat = format;
            item.Resolution = resolution;
            await _repository.AddAsync(item, CancellationToken.None);
        }
    }
}
