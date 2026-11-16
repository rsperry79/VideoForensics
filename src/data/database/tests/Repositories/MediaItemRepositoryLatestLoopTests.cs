using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// The Devices tab's loop is the device's newest roll-up loop even when snapshots arrive faster than the recent-media window.
    /// </summary>
    public class MediaItemRepository_LatestLoop_Tests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        private const string LoopMetadata = "{\"type\":\"snapshot-rollup\"}";
        private const string SnapshotMetadata = "{\"type\":\"snapshot\",\"source\":\"capture\"}";

        private SqliteInMemoryFixture _fixture = null!;
        private MediaItemRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var unitOfWork = new UnitOfWork(_fixture.Factory, new TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>());
            _repository = new MediaItemRepository(unitOfWork, NullLogger<MediaItemRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static MediaItem Row(Guid deviceId, string format, DateTime recordedAt, string metadata)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = $"{format}-{recordedAt:HHmmss}",
                FilePath = $"{format}-{recordedAt:HHmmss}",
                MediaFormat = format,
                Sha256Hash = Guid.NewGuid().ToString("N"),
                RecordedAtUtc = recordedAt,
                DownloadedAtUtc = recordedAt,
                MetadataJson = metadata,
            };
        }

        [Fact]
        public async Task MediaItemRepository_LatestLoop_ReturnsLoopOlderThanTheNewestSnapshots()
        {
            var deviceId = Guid.NewGuid();
            MediaItem loop = Row(deviceId, "mp4", Start, LoopMetadata);
            await _repository.AddAsync(loop, CancellationToken.None);
            for (int i = 1; i <= 250; i++)
            {
                await _repository.AddAsync(Row(deviceId, "jpg", Start.AddSeconds(i * 5), SnapshotMetadata), CancellationToken.None);
            }

            MediaItem? latest = await _repository.GetLatestRollupLoopAsync(deviceId, CancellationToken.None);

            Assert.NotNull(latest);
            Assert.Equal(loop.Id, latest!.Id);
        }

        [Fact]
        public async Task MediaItemRepository_LatestLoop_IgnoresOtherDevicesAndNonLoops()
        {
            var deviceId = Guid.NewGuid();
            await _repository.AddAsync(Row(Guid.NewGuid(), "mp4", Start.AddMinutes(1), LoopMetadata), CancellationToken.None);
            await _repository.AddAsync(Row(deviceId, "mp4", Start.AddMinutes(2), "{\"type\":\"video\"}"), CancellationToken.None);

            MediaItem? latest = await _repository.GetLatestRollupLoopAsync(deviceId, CancellationToken.None);

            Assert.Null(latest);
        }
    }
}
