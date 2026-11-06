using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    /// <summary>The per-second bitrate series is read from the clip when saved, otherwise computed from its packets and saved on the clip.</summary>
    public class MediaBitrateSeriesServiceTests
    {
        private readonly Mock<IMediaItemRepository> _items = new();
        private readonly Mock<IMediaPacketReader> _reader = new();
        private readonly Mock<IMediaPathResolver> _paths = new();

        private MediaBitrateSeriesService Sut()
        {
            return new(_items.Object, _reader.Object, _paths.Object, NullLogger<MediaBitrateSeriesService>.Instance);
        }

        private MediaItem Clip(string? savedSeries = null)
        {
            var clip = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "clip.mp4",
                FilePath = "clip.mp4",
                MediaFormat = "mp4",
                Sha256Hash = "0",
                RecordedAtUtc = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc),
                BitrateSeriesJson = savedSeries,
            };
            _ = _items.Setup(r => r.GetAsync(clip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
            _ = _paths.Setup(p => p.ToAbsoluteAsync("clip.mp4", It.IsAny<CancellationToken>())).ReturnsAsync("/media/clip.mp4");
            return clip;
        }

        [Fact]
        public async Task MediaBitrateSeriesService_SavedSeries_ReturnsItWithoutReadingPackets()
        {
            MediaItem clip = Clip("[{\"Second\":0,\"Kbps\":8},{\"Second\":1,\"Kbps\":16}]");

            IReadOnlyList<BitrateSecond>? series = await Sut().GetAsync(clip.Id, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(2, series!.Count);
            Assert.Equal(16, series[1].Kbps);
            _reader.Verify(r => r.ReadVideoPacketsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MediaBitrateSeriesService_NotSaved_ComputesAndSavesOnTheClip()
        {
            MediaItem clip = Clip();
            _ = _reader.Setup(r => r.ReadVideoPacketsAsync("/media/clip.mp4", It.IsAny<CancellationToken>()))
                .ReturnsAsync([(0.2, 1000L), (1.4, 500L)]);

            IReadOnlyList<BitrateSecond>? series = await Sut().GetAsync(clip.Id, CancellationToken.None);

            Assert.NotNull(series);
            Assert.Equal(2, series!.Count);
            Assert.Equal(8, series[0].Kbps);
            _items.Verify(r => r.UpdateAsync(It.Is<MediaItem>(m => m.Id == clip.Id && m.BitrateSeriesJson != null && m.BitrateSeriesJson.Contains("\"Kbps\":8")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MediaBitrateSeriesService_UnreadableFile_ReturnsNullAndSavesNothing()
        {
            MediaItem clip = Clip();
            _ = _reader.Setup(r => r.ReadVideoPacketsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<(double PtsSeconds, long SizeBytes)>?)null);

            IReadOnlyList<BitrateSecond>? series = await Sut().GetAsync(clip.Id, CancellationToken.None);

            Assert.Null(series);
            _items.Verify(r => r.UpdateAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MediaBitrateSeriesService_UnknownClip_ReturnsNull()
        {
            Assert.Null(await Sut().GetAsync(Guid.NewGuid(), CancellationToken.None));
        }
    }
}
