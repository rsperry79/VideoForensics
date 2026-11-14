using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    /// <summary>The codec backfill fills codec fields from ffprobe and skips an item ffprobe cannot read, without re-probing it.</summary>
    public class MediaCodecBackfillTests
    {
        private readonly Mock<IMediaItemWriteRepository> _items = new();
        private readonly Mock<IMediaBitrateAnalyzer> _bitrate = new();
        private readonly Mock<IMediaPathResolver> _paths = new();

        private MediaCodecBackfill Sut()
        {
            _ = _paths.Setup(p => p.ToAbsoluteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) => path);
            return new(_items.Object, _bitrate.Object, _paths.Object, NullLogger<MediaCodecBackfill>.Instance);
        }

        private static MediaItem Item(string name)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = name,
                FilePath = name,
                MediaFormat = "mp4",
                Sha256Hash = "h",
                FileSizeBytes = 1000,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
            };
        }

        [Fact]
        public async Task BackfillAsync_FillsCodecFieldsAndSkipsAnItemFfprobeCannotRead()
        {
            MediaItem readable = Item("readable.mp4");
            MediaItem unreadable = Item("unreadable.mp4");
            _ = _items.Setup(r => r.ListMissingCodecAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([readable, unreadable]);
            _ = _bitrate.Setup(b => b.AnalyzeAsync("readable.mp4", 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaBitrateAnalysis(1_000_000, 60, false, "h264", "aac", "1920x1080", 30m));
            _ = _bitrate.Setup(b => b.AnalyzeAsync("unreadable.mp4", 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaBitrateAnalysis?)null);

            int filled = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(1, filled);
            Assert.Equal("h264", readable.VideoCodec);
            Assert.Equal("aac", readable.AudioCodec);
            Assert.Equal("1920x1080", readable.Resolution);
            Assert.Equal(30m, readable.FrameRate);
            Assert.Null(unreadable.VideoCodec);
            _items.Verify(r => r.UpdateAsync(readable, It.IsAny<CancellationToken>()), Times.Once);
            _items.Verify(r => r.UpdateAsync(unreadable, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task BackfillAsync_SecondPass_DoesNotProbeAnItemFfprobeCouldNotRead()
        {
            MediaItem unreadable = Item("unreadable.mp4");
            _ = _items.Setup(r => r.ListMissingCodecAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([unreadable]);
            _ = _bitrate.Setup(b => b.AnalyzeAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaBitrateAnalysis?)null);

            MediaCodecBackfill sut = Sut();
            _ = await sut.BackfillAsync(CancellationToken.None);
            _ = await sut.BackfillAsync(CancellationToken.None);

            _bitrate.Verify(b => b.AnalyzeAsync("unreadable.mp4", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
