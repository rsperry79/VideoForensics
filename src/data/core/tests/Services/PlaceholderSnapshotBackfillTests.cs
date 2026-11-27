using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests.Services
{
    /// <summary>The placeholder backfill loops over batches until none remain, sums the marked rows, and stops on failure.</summary>
    public class PlaceholderSnapshotBackfillTests
    {
        private readonly Mock<IMediaItemWriteRepository> _items = new();
        private readonly CapturingLogger<PlaceholderSnapshotBackfill> _logger = new();

        private PlaceholderSnapshotBackfill Sut()
        {
            return new(_items.Object, _logger);
        }

        [Fact]
        public async Task BackfillAsync_LoopsUntilBatchReturnsZero_SumsEveryBatch()
        {
            _ = _items.SetupSequence(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(500)
                .ReturnsAsync(200)
                .ReturnsAsync(0);

            int marked = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(700, marked);
            _items.Verify(r => r.MarkRepeatSnapshotPlaceholdersAsync(500, It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task BackfillAsync_NothingToMark_ReturnsZeroAfterOneBatch()
        {
            _ = _items.Setup(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            int marked = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(0, marked);
            _items.Verify(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_Success_LogsOneInformationWithTotal()
        {
            _ = _items.SetupSequence(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(500)
                .ReturnsAsync(0);

            _ = await Sut().BackfillAsync(CancellationToken.None);

            string info = Assert.Single(_logger.Entries.Where(e => e.Level == LogLevel.Information)).Message;
            Assert.Contains("500", info);
            Assert.Empty(_logger.Entries.Where(e => e.Level == LogLevel.Error));
        }

        [Fact]
        public async Task BackfillAsync_CancelledBeforeStart_ThrowsAndCallsNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut().BackfillAsync(cts.Token));

            _items.Verify(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task BackfillAsync_CancelledBetweenBatches_StopsAfterCurrentBatch()
        {
            using var cts = new CancellationTokenSource();
            _ = _items.Setup(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    cts.Cancel();
                    return Task.FromResult(500);
                });

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut().BackfillAsync(cts.Token));

            _items.Verify(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_RepositoryThrows_LogsErrorWithTypeAndReturnsTotalSoFar()
        {
            _ = _items.SetupSequence(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(500)
                .ThrowsAsync(new InvalidOperationException("disk gone"));

            int marked = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(500, marked);
            _items.Verify(r => r.MarkRepeatSnapshotPlaceholdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            string error = Assert.Single(_logger.Entries.Where(e => e.Level == LogLevel.Error)).Message;
            Assert.Contains("InvalidOperationException", error);
            Assert.Contains("disk gone", error);
        }

        /// <summary>Keeps each log entry's level and formatted text, so a test can count and read them.</summary>
        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            IDisposable? ILogger.BeginScope<TState>(TState state) => null;

            bool ILogger.IsEnabled(LogLevel logLevel) => true;

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
