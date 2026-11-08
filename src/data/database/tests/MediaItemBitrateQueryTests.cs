using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The measured-bitrate query keeps only stream-reported bitrates, for one device, oldest first, filtered in SQL.</summary>
    public class MediaItemBitrateQueryTests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

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

        private static MediaItem Row(Guid deviceId, string name, DateTime recordedAt, long? bps, bool? estimated, string? lighting = null, string? flags = null)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = name,
                FilePath = name,
                MediaFormat = "mp4",
                Sha256Hash = Guid.NewGuid().ToString("N"),
                RecordedAtUtc = recordedAt,
                DownloadedAtUtc = recordedAt,
                VideoBitrateBps = bps,
                BitrateEstimated = estimated,
                LightingMode = lighting,
                RedFlags = flags,
            };
        }

        [Fact]
        public async Task GetMeasuredBitrateByDeviceAsync_SkipsEstimatedAndUnmeasuredAndOtherDevices_OldestFirst()
        {
            var device = Guid.NewGuid();
            MediaItem late = Row(device, "late", Start.AddMinutes(9), 2_000_000, false, "Day", "BitrateCollapse,ClippedAudio");
            MediaItem early = Row(device, "early", Start.AddMinutes(1), 1_000_000, false, "Ir");
            await _repository.AddAsync(late, CancellationToken.None);
            await _repository.AddAsync(early, CancellationToken.None);
            await _repository.AddAsync(Row(device, "null-flag", Start.AddMinutes(5), 1_500_000, null), CancellationToken.None);
            await _repository.AddAsync(Row(device, "estimated", Start.AddMinutes(2), 900_000, true), CancellationToken.None);
            await _repository.AddAsync(Row(device, "unmeasured", Start.AddMinutes(3), null, null), CancellationToken.None);
            await _repository.AddAsync(Row(Guid.NewGuid(), "other-device", Start.AddMinutes(4), 800_000, false), CancellationToken.None);

            IReadOnlyList<MeasuredBitrate> rows = await _repository.GetMeasuredBitrateByDeviceAsync(device, CancellationToken.None);

            Assert.Equal(new long[] { 1_000_000, 1_500_000, 2_000_000 }, rows.Select(r => r.VideoBitrateBps).ToArray());
            Assert.Equal("Ir", rows[0].LightingMode);
            Assert.Equal("BitrateCollapse,ClippedAudio", rows[2].RedFlags);
            Assert.Equal(Start.AddMinutes(1), rows[0].RecordedAtUtc);
            Assert.Equal(early.Id, rows[0].MediaItemId);
            Assert.Equal(late.Id, rows[2].MediaItemId);
        }

        [Fact]
        public async Task GetMeasuredBitrateByDeviceAsync_ExcludesTimeLapseRollups_KeepsCameraClips()
        {
            var device = Guid.NewGuid();
            MediaItem clip = Row(device, "clip", Start.AddMinutes(1), 1_000_000, false);
            MediaItem lapse = Row(device, "lapse", Start.AddMinutes(2), 300_000, false);
            lapse.MetadataJson = "{\"type\":\"snapshot-rollup\"}";
            await _repository.AddAsync(clip, CancellationToken.None);
            await _repository.AddAsync(lapse, CancellationToken.None);

            IReadOnlyList<MeasuredBitrate> rows = await _repository.GetMeasuredBitrateByDeviceAsync(device, CancellationToken.None);

            Assert.Equal(new long[] { 1_000_000 }, rows.Select(r => r.VideoBitrateBps).ToArray());
            Assert.Equal(clip.Id, rows[0].MediaItemId);
        }

        [Fact]
        public async Task GetBitrateBaselineByDeviceAsync_CountsReadingsOutsideAnyWindow_WithSampleStdDev()
        {
            var device = Guid.NewGuid();
            // Two readings are more than a year before the others, so no date window could hold all three.
            await _repository.AddAsync(Row(device, "old", Start.AddDays(-400), 2_000_000, false), CancellationToken.None);
            await _repository.AddAsync(Row(device, "early", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1_000_000, false), CancellationToken.None);
            await _repository.AddAsync(Row(device, "recent", Start, 3_000_000, false), CancellationToken.None);
            await _repository.AddAsync(Row(device, "estimated", Start.AddMinutes(2), 9_000_000, true), CancellationToken.None);
            await _repository.AddAsync(Row(device, "unmeasured", Start.AddMinutes(3), null, null), CancellationToken.None);
            await _repository.AddAsync(Row(Guid.NewGuid(), "other-device", Start.AddMinutes(4), 9_000_000, false), CancellationToken.None);

            AllTimeBitrateBaseline baseline = await _repository.GetBitrateBaselineByDeviceAsync(device, CancellationToken.None);

            // Readings 1, 2 and 3 million bps: mean 2 million, sample sd (divides by n - 1) exactly 1 million.
            Assert.Equal(3, baseline.Count);
            Assert.Equal(2_000_000.0, baseline.MeanBps, precision: 3);
            Assert.NotNull(baseline.StdDevBps);
            Assert.Equal(1_000_000.0, baseline.StdDevBps!.Value, precision: 3);
        }
    }
}
