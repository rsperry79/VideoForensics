using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>The backfill raises one BitrateCollapse flag per flagged clip in the window and keeps going past a clip that throws.</summary>
    public class MediaFlagBackfillTests
    {
        private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IMediaItemRepository> _items = new();
        private readonly Mock<IMediaFlagRepository> _flags = new();

        private MediaFlagBackfill Sut()
        {
            return new(_items.Object, _flags.Object, new FixedClock(Now), NullLogger<MediaFlagBackfill>.Instance);
        }

        private static MediaItem Clip(string name, DateTime recordedAtUtc)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = name,
                FilePath = name,
                MediaFormat = "mp4",
                Sha256Hash = "h",
                RecordedAtUtc = recordedAtUtc,
                DownloadedAtUtc = recordedAtUtc,
                DurationSeconds = 30,
                RedFlags = "BitrateCollapse",
            };
        }

        [Fact]
        public async Task BackfillAsync_FlaggedClipInWindow_UpsertsBitrateCollapseFlag()
        {
            MediaItem clip = Clip("a.mp4", Now.AddDays(-2));
            _ = _items.Setup(r => r.ListFlaggedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([clip]);

            int upserted = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(1, upserted);
            _flags.Verify(f => f.UpsertRaisedAsync(
                It.Is<MediaFlag>(m => m.FlagType == MediaFlagType.BitrateCollapse && m.MediaItemId == clip.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_ClipOlderThanWindow_WritesNothing()
        {
            MediaItem old = Clip("old.mp4", Now.AddDays(-10));
            MediaItem[] all = [old];
            // The fake applies the same since filter as the repository query, so an old clip is never returned.
            _ = _items.Setup(r => r.ListFlaggedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DateTime since, CancellationToken _) => all.Where(c => c.RecordedAtUtc >= since).ToList());

            int upserted = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(0, upserted);
            _items.Verify(r => r.ListFlaggedSinceAsync(Now.AddDays(-MediaFlagBackfill.WindowDays), It.IsAny<CancellationToken>()), Times.Once);
            _flags.Verify(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task BackfillAsync_OneClipThrows_ContinuesWithTheRest()
        {
            MediaItem bad = Clip("bad.mp4", Now.AddDays(-1));
            MediaItem good = Clip("good.mp4", Now.AddDays(-2));
            _ = _items.Setup(r => r.ListFlaggedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([bad, good]);
            _ = _flags.Setup(f => f.UpsertRaisedAsync(It.Is<MediaFlag>(m => m.MediaItemId == bad.Id), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("write failed"));

            int upserted = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(1, upserted);
            _flags.Verify(f => f.UpsertRaisedAsync(It.Is<MediaFlag>(m => m.MediaItemId == good.Id), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BackfillAsync_StillClip_IsNotUpsertedButIsReconciled()
        {
            MediaItem still = Clip("snap.jpg", Now.AddDays(-2));
            _ = _items.Setup(r => r.ListFlaggedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([still]);

            int upserted = await Sut().BackfillAsync(CancellationToken.None);

            Assert.Equal(0, upserted);
            _flags.Verify(f => f.UpsertRaisedAsync(It.IsAny<MediaFlag>(), It.IsAny<CancellationToken>()), Times.Never);
            _flags.Verify(f => f.ReconcileForMediaAsync(
                still.Id,
                It.Is<IReadOnlySet<MediaFlagType>>(s => s.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>A clock frozen at one instant, so the since boundary is exact.</summary>
        private sealed class FixedClock(DateTime utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return new DateTimeOffset(utcNow, TimeSpan.Zero);
            }
        }
    }
}
