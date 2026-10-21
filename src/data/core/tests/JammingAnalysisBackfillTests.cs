using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>One backfill pass analyses the unanalysed batch and keeps going past an item that throws.</summary>
    public class JammingAnalysisBackfillTests
    {
        private readonly Mock<IMediaItemRepository> _items = new();
        private readonly Mock<IMediaAnalysisService> _analysis = new();

        private JammingAnalysisBackfill Sut() => new(_items.Object, _analysis.Object, NullLogger<JammingAnalysisBackfill>.Instance);

        private static MediaItem Item(string name) => new()
        {
            Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = name, FilePath = name, MediaFormat = "mp4", Sha256Hash = "h",
            RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow,
        };

        [Fact]
        public async Task BackfillAsync_AnalysesEveryUnanalysedItemInTheBatch()
        {
            MediaItem first = Item("a.mp4");
            MediaItem second = Item("b.jpg");
            _items.Setup(r => r.ListUnanalyzedAsync(JammingAnalysisBackfill.BatchSize, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { first, second });

            int analysed = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(2, analysed);
            _analysis.Verify(a => a.AnalyzeAsync(first.Id, It.IsAny<CancellationToken>()), Times.Once);
            _analysis.Verify(a => a.AnalyzeAsync(second.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_ItemThrows_SkipsItAndContinues()
        {
            MediaItem bad = Item("bad.mp4");
            MediaItem good = Item("good.mp4");
            _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { bad, good });
            _analysis.Setup(a => a.AnalyzeAsync(bad.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("ffprobe failed"));

            int analysed = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(1, analysed);
            _analysis.Verify(a => a.AnalyzeAsync(good.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_NothingUnanalysed_DoesNothing()
        {
            _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<MediaItem>());

            Assert.Equal(0, await Sut().BackfillAsync(CancellationToken.None));
            _analysis.Verify(a => a.AnalyzeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
