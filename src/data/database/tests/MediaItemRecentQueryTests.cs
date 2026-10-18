using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class MediaItemRecentQueryTests : IAsyncLifetime
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

        private static MediaItem Clip(Guid deviceId, int daysAgo) => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = $"clip-{daysAgo}.mp4",
            FilePath = $"clip-{daysAgo}.mp4",
            MediaFormat = "mp4",
            Sha256Hash = Guid.NewGuid().ToString("N"),
            RecordedAtUtc = DateTime.UtcNow.AddDays(-daysAgo),
            DownloadedAtUtc = DateTime.UtcNow
        };

        [Fact]
        public async Task GetRecentByDeviceAsync_ReturnsNewestClipsUpToLimitForThatDeviceOnly()
        {
            Guid device = Guid.NewGuid();
            Guid other = Guid.NewGuid();
            foreach (int days in new[] { 5, 1, 3, 2, 4 })
            {
                await _repository.AddAsync(Clip(device, days), CancellationToken.None);
            }

            await _repository.AddAsync(Clip(other, 0), CancellationToken.None);

            IReadOnlyList<MediaItem> recent = await _repository.GetRecentByDeviceAsync(device, limit: 3, CancellationToken.None);

            Assert.Equal(3, recent.Count);
            Assert.All(recent, m => Assert.Equal(device, m.DeviceId));
            Assert.Equal(new[] { "clip-1.mp4", "clip-2.mp4", "clip-3.mp4" }, recent.Select(m => m.FileName).ToArray());
        }
    }
}
