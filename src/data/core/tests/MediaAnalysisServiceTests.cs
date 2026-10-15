using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class MediaAnalysisServiceTests
    {
        private readonly Mock<IMediaItemRepository> _items = new();
        private readonly Mock<IMediaBitrateAnalyzer> _bitrate = new();
        private readonly Mock<IClipColorAnalyzer> _color = new();
        private readonly Mock<IAudioClipAnalyzer> _audio = new();
        private readonly Guid _deviceId = Guid.NewGuid();

        private MediaAnalysisService CreateSut() => new(_items.Object, _bitrate.Object, _color.Object, _audio.Object, NullLogger<MediaAnalysisService>.Instance);

        private static MediaItem Clip(Guid deviceId, string mode, long bitrate, bool flagged = false) => new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "c.mp4",
            FilePath = "c.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "h",
            FileSizeBytes = 1_000_000,
            RecordedAtUtc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            LightingMode = mode,
            VideoBitrateBps = bitrate,
            BitrateEstimated = false,
            NoiseFloorDb = -70.0,
            SilenceFraction = 0.0,
            RedFlags = flagged ? "AudioDropout" : string.Empty
        };

        private void SetUpClip(MediaItem clip, IReadOnlyList<MediaItem> earlier, MediaBitrateAnalysis bitrate, ClipColorStats color, AudioClipStats audio)
        {
            _items.Setup(r => r.GetAsync(clip.Id, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
            _items.Setup(r => r.GetRecentByDeviceAsync(clip.DeviceId, MediaAnalysisService.HistoryWindowClips, It.IsAny<CancellationToken>())).ReturnsAsync(earlier);
            _bitrate.Setup(a => a.AnalyzeAsync(clip.FilePath, clip.FileSizeBytes, It.IsAny<CancellationToken>())).ReturnsAsync(bitrate);
            _color.Setup(a => a.AnalyzeAsync(clip.FilePath, It.IsAny<CancellationToken>())).ReturnsAsync(color);
            _audio.Setup(a => a.AnalyzeAsync(clip.FilePath, bitrate.DurationSeconds, It.IsAny<CancellationToken>())).ReturnsAsync(audio);
        }

        [Fact]
        public async Task AnalyzeAsync_CollapsedBitrateAndClippedAudio_FlagsJamming()
        {
            MediaItem clip = Clip(_deviceId, "Day", 0);
            List<MediaItem> earlier = Enumerable.Range(0, 12).Select(_ => Clip(_deviceId, "Ir", 1_000_000)).ToList();
            SetUpClip(clip, earlier, new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                new ClipColorStats(0.5, 25.0), new AudioClipStats(50, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            Assert.Equal("Ir", clip.LightingMode);
            Assert.Equal(300_000, clip.VideoBitrateBps);
            Assert.Contains("BitrateCollapse", clip.RedFlags);
            Assert.Contains("ClippedAudio", clip.RedFlags);
            Assert.True(clip.JammingSuspected);
            _items.Verify(r => r.UpdateAsync(clip, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeAsync_IrClipNotComparedWithDayBaseline()
        {
            // Day clips run at 4 Mb/s; an IR clip at 300 kb/s must not be called a collapse against them.
            MediaItem clip = Clip(_deviceId, "Day", 0);
            List<MediaItem> earlier = Enumerable.Range(0, 12).Select(_ => Clip(_deviceId, "Day", 4_000_000)).ToList();
            SetUpClip(clip, earlier, new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                new ClipColorStats(0.5, 25.0), new AudioClipStats(0, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            Assert.DoesNotContain("BitrateCollapse", clip.RedFlags ?? string.Empty);
        }

        [Fact]
        public async Task AnalyzeAsync_NoPriorClips_RaisesNoBaselineFlags()
        {
            MediaItem clip = Clip(_deviceId, "Ir", 0);
            SetUpClip(clip, Array.Empty<MediaItem>(), new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                new ClipColorStats(0.5, 25.0), new AudioClipStats(0, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            Assert.Equal(string.Empty, clip.RedFlags);
            Assert.False(clip.JammingSuspected);
        }

        [Fact]
        public async Task AnalyzeAsync_ColourAnalysisUnavailable_LeavesFlagsNotEvaluated()
        {
            MediaItem clip = Clip(_deviceId, "Day", 0);
            SetUpClip(clip, Array.Empty<MediaItem>(), new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                color: null!, new AudioClipStats(0, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            // Not evaluated is different from clean: a null here must not look like "no flags".
            Assert.Null(clip.RedFlags);
            Assert.Null(clip.JammingSuspected);
        }

        [Fact]
        public async Task AnalyzeAsync_UnknownStoredLightingMode_IsSkippedAndAnalysisStillCompletes()
        {
            MediaItem clip = Clip(_deviceId, "Ir", 0);
            List<MediaItem> earlier = Enumerable.Range(0, 12).Select(_ => Clip(_deviceId, "Ir", 1_000_000)).ToList();
            earlier.Add(Clip(_deviceId, "Legacy", 4_000_000));
            SetUpClip(clip, earlier, new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                new ClipColorStats(0.5, 25.0), new AudioClipStats(0, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            Assert.NotNull(clip.RedFlags);
            Assert.Contains("BitrateCollapse", clip.RedFlags);
        }

        [Fact]
        public async Task AnalyzeAsync_EstimatedBitrateNotComparedWithMeasuredBaseline()
        {
            // Earlier clips only have estimated bitrates (size / duration, which includes audio and container overhead).
            // A measured stream bitrate must not be judged against them.
            MediaItem clip = Clip(_deviceId, "Ir", 0);
            List<MediaItem> earlier = Enumerable.Range(0, 12).Select(_ => Clip(_deviceId, "Ir", 4_000_000)).ToList();
            foreach (MediaItem e in earlier)
            {
                e.BitrateEstimated = true;
            }

            SetUpClip(clip, earlier, new MediaBitrateAnalysis(300_000, 30.0, BitrateEstimated: false),
                new ClipColorStats(0.5, 25.0), new AudioClipStats(0, -70.0, 0, 0.0));

            await CreateSut().AnalyzeAsync(clip.Id, CancellationToken.None);

            Assert.DoesNotContain("BitrateCollapse", clip.RedFlags ?? string.Empty);
        }
    }
}
