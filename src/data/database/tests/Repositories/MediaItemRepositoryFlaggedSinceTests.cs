using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The bit rate backfill query returns only clips flagged BitrateCollapse, recorded at or after the cut-off, oldest first.</summary>
    public class MediaItemRepository_FlaggedSince_Tests : IAsyncLifetime
    {
        private static readonly DateTime Since = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

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

        private static MediaItem Row(string name, DateTime recordedAt, string? flags)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = name,
                FilePath = name,
                MediaFormat = "mp4",
                Sha256Hash = Guid.NewGuid().ToString("N"),
                RecordedAtUtc = recordedAt,
                DownloadedAtUtc = recordedAt,
                RedFlags = flags,
            };
        }

        [Fact]
        public async Task MediaItemRepository_FlaggedSince_ReturnsOnlyBitrateCollapseInWindow()
        {
            MediaItem inWindow = Row("in-window", Since.AddMinutes(5), "BitrateCollapse");
            await _repository.AddAsync(inWindow, CancellationToken.None);
            await _repository.AddAsync(Row("before-window", Since.AddMinutes(-1), "BitrateCollapse"), CancellationToken.None);
            await _repository.AddAsync(Row("other-flag", Since.AddMinutes(6), "ClippedAudio"), CancellationToken.None);

            IReadOnlyList<MediaItem> rows = await _repository.ListFlaggedSinceAsync(Since, CancellationToken.None);

            MediaItem only = Assert.Single(rows);
            Assert.Equal(inWindow.Id, only.Id);
        }

        [Fact]
        public async Task MediaItemRepository_FlaggedSince_OrdersByRecordedAt()
        {
            MediaItem later = Row("later", Since.AddMinutes(9), "BitrateCollapse");
            MediaItem earlier = Row("earlier", Since.AddMinutes(2), "BitrateCollapse");
            await _repository.AddAsync(later, CancellationToken.None);
            await _repository.AddAsync(earlier, CancellationToken.None);

            IReadOnlyList<MediaItem> rows = await _repository.ListFlaggedSinceAsync(Since, CancellationToken.None);

            Assert.Equal(new[] { earlier.Id, later.Id }, rows.Select(r => r.Id).ToArray());
        }

        [Fact]
        public async Task MediaItemRepository_FlaggedSince_EmptyWhenNothingMatches()
        {
            await _repository.AddAsync(Row("no-flags", Since.AddMinutes(1), null), CancellationToken.None);
            await _repository.AddAsync(Row("other-flag", Since.AddMinutes(2), "ClippedAudio"), CancellationToken.None);
            await _repository.AddAsync(Row("before-window", Since.AddMinutes(-3), "BitrateCollapse"), CancellationToken.None);

            IReadOnlyList<MediaItem> rows = await _repository.ListFlaggedSinceAsync(Since, CancellationToken.None);

            Assert.Empty(rows);
        }
    }
}
