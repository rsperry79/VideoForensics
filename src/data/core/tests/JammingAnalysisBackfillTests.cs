using Microsoft.Extensions.Logging;
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

        private JammingAnalysisBackfill Sut()
        {
            return new(_items.Object, _analysis.Object, NullLogger<JammingAnalysisBackfill>.Instance);
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
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
            };
        }

        [Fact]
        public async Task BackfillAsync_AnalysesEveryUnanalysedItemInTheBatch()
        {
            MediaItem first = Item("a.mp4");
            MediaItem second = Item("b.jpg");
            _ = _items.Setup(r => r.ListUnanalyzedAsync(JammingAnalysisBackfill.BatchSize, It.IsAny<CancellationToken>()))
                .ReturnsAsync([first, second]);

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
            _ = _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([bad, good]);
            _ = _analysis.Setup(a => a.AnalyzeAsync(bad.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("ffprobe failed"));

            int analysed = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(1, analysed);
            _analysis.Verify(a => a.AnalyzeAsync(good.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_NothingUnanalysed_DoesNothing()
        {
            _ = _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

            Assert.Equal(0, await Sut().BackfillAsync(CancellationToken.None));
            _analysis.Verify(a => a.AnalyzeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task BackfillAsync_LogsRecordedTimeRangeOfAnalysedItems()
        {
            var logger = new CapturingLogger<JammingAnalysisBackfill>();
            MediaItem early = Item("early.mp4");
            early.RecordedAtUtc = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc);
            MediaItem late = Item("late.mp4");
            late.RecordedAtUtc = new DateTime(2026, 1, 3, 12, 45, 0, DateTimeKind.Utc);
            _ = _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([early, late]);

            _ = await new JammingAnalysisBackfill(_items.Object, _analysis.Object, logger).BackfillAsync(CancellationToken.None);

            Assert.Contains("analysed 2 of 2", logger.LastMessage);
            Assert.Contains("recorded 2026-01-01 00:30 to 2026-01-03 12:45 UTC", logger.LastMessage);
        }

        [Fact]
        public async Task BackfillAsync_RangeLeavesOutItemsThatFailed()
        {
            var logger = new CapturingLogger<JammingAnalysisBackfill>();
            MediaItem failed = Item("failed.mp4");
            failed.RecordedAtUtc = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc);
            MediaItem analysed = Item("ok.mp4");
            analysed.RecordedAtUtc = new DateTime(2026, 1, 2, 9, 15, 0, DateTimeKind.Utc);
            _ = _items.Setup(r => r.ListUnanalyzedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([failed, analysed]);
            _ = _analysis.Setup(a => a.AnalyzeAsync(failed.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("ffprobe failed"));

            _ = await new JammingAnalysisBackfill(_items.Object, _analysis.Object, logger).BackfillAsync(CancellationToken.None);

            Assert.Contains("recorded 2026-01-02 09:15 to 2026-01-02 09:15 UTC", logger.LastMessage);
        }

        /// <summary>Keeps the last formatted information message so a test can assert on what the operator will read.</summary>
        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public string LastMessage { get; private set; } = string.Empty;

            IDisposable? ILogger.BeginScope<TState>(TState state)
            {
                return null;
            }

            bool ILogger.IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Information)
                {
                    LastMessage = formatter(state, exception);
                }
            }
        }
    }
}
