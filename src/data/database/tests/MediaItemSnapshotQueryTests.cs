using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The snapshot query filters format, the snapshot marker and the window in SQL, oldest first.</summary>
    public class MediaItemSnapshotQueryTests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private const string SnapshotMetadata = "{\"eventId\":\"snapshot-1\",\"type\":\"snapshot\"}";

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

        private static MediaItem Row(Guid deviceId, string name, string format, DateTime recordedAt, string? metadata) => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = name,
            FilePath = name,
            MediaFormat = format,
            Sha256Hash = Guid.NewGuid().ToString("N"),
            RecordedAtUtc = recordedAt,
            DownloadedAtUtc = recordedAt,
            MetadataJson = metadata,
        };

        [Fact]
        public async Task GetSnapshotsByDeviceAndDateRangeAsync_ReturnsOnlySnapshotJpegsInWindowOldestFirst()
        {
            Guid device = Guid.NewGuid();
            await _repository.AddAsync(Row(device, "late.jpg", "jpg", Start.AddMinutes(9), SnapshotMetadata), CancellationToken.None);
            await _repository.AddAsync(Row(device, "early.jpg", "jpg", Start.AddMinutes(1), SnapshotMetadata), CancellationToken.None);
            await _repository.AddAsync(Row(device, "event.jpg", "jpg", Start.AddMinutes(2), "{\"type\":\"event\"}"), CancellationToken.None);
            await _repository.AddAsync(Row(device, "clip.mp4", "mp4", Start.AddMinutes(3), SnapshotMetadata), CancellationToken.None);
            await _repository.AddAsync(Row(device, "outside.jpg", "jpg", Start.AddHours(2), SnapshotMetadata), CancellationToken.None);

            IReadOnlyList<MediaItem> rows = await _repository.GetSnapshotsByDeviceAndDateRangeAsync(device, Start, Start.AddMinutes(15), CancellationToken.None);

            Assert.Equal(new[] { "early.jpg", "late.jpg" }, rows.Select(r => r.FileName).ToArray());
        }
    }
}
