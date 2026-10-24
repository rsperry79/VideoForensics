using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Unanalysed media is the rows with no baseline version, oldest first; purged rows are excluded.</summary>
    public class MediaItemUnanalyzedQueryTests : IAsyncLifetime
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

        private static MediaItem Row(string name, DateTime recordedAt, int? baselineVersion)
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
                BaselineVersion = baselineVersion,
            };
        }

        [Fact]
        public async Task ListUnanalyzedAsync_ReturnsOnlyNeverAnalysedRowsOldestFirst()
        {
            DateTime now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
            await _repository.AddAsync(Row("newer.mp4", now, null), CancellationToken.None);
            await _repository.AddAsync(Row("analysed.mp4", now.AddMinutes(-1), 3), CancellationToken.None);
            await _repository.AddAsync(Row("older.mp4", now.AddMinutes(-5), null), CancellationToken.None);

            IReadOnlyList<MediaItem> rows = await _repository.ListUnanalyzedAsync(10, CancellationToken.None);

            Assert.Equal(new[] { "older.mp4", "newer.mp4" }, rows.Select(r => r.FileName).ToArray());
        }

        [Fact]
        public async Task ListUnanalyzedAsync_RespectsTheLimit()
        {
            DateTime now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 4; i++)
            {
                await _repository.AddAsync(Row($"clip-{i}.mp4", now.AddMinutes(i), null), CancellationToken.None);
            }

            Assert.Equal(2, (await _repository.ListUnanalyzedAsync(2, CancellationToken.None)).Count);
        }
    }
}
