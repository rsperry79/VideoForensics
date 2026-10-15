using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class AudioClipStatsTests
    {
        private const string Astats = """
            lavfi.astats.Overall.Peak_count=12
            lavfi.astats.Overall.Peak_level=-0.05
            lavfi.astats.Overall.Noise_floor=-72.5
            lavfi.astats.Overall.Peak_count=40
            lavfi.astats.Overall.Peak_level=-0.02
            lavfi.astats.Overall.Noise_floor=-70.0
            """;

        private const string Silence = """
            [silencedetect @ 0x1] silence_start: 1.0
            [silencedetect @ 0x1] silence_end: 1.5 | silence_duration: 0.5
            [silencedetect @ 0x1] silence_end: 3.0 | silence_duration: 0.1
            [silencedetect @ 0x1] silence_start: 8.0
            [silencedetect @ 0x1] silence_end: 9.0 | silence_duration: 1.0
            """;

        [Fact]
        public void Parse_ReportsPeakCountNoiseFloorAndSilentGaps()
        {
            AudioClipStats? stats = AudioStatsParser.Parse(Astats, Silence, durationSeconds: 10.0);

            Assert.NotNull(stats);
            Assert.Equal(40, stats.ClippedPeakCount);        // cumulative: the largest reported count
            Assert.Equal(-70.0, stats.NoiseFloorDb);         // the last reported floor
            Assert.Equal(2, stats.SilentGapCount);           // 0.1 s gap is below the minimum dropout length
            Assert.NotNull(stats.SilenceFraction);
            Assert.Equal(0.16, stats.SilenceFraction.Value, 3); // all silent time counts: (0.5 + 1.0 + 0.1) / 10; only the gap count drops short gaps
        }

        [Fact]
        public void Parse_NoDuration_LeavesSilenceFractionNull()
        {
            AudioClipStats? stats = AudioStatsParser.Parse(Astats, Silence, durationSeconds: null);

            Assert.NotNull(stats);
            Assert.Null(stats.SilenceFraction);
        }

        [Fact]
        public void Parse_NoAstatsOutput_ReturnsNull()
        {
            Assert.Null(AudioStatsParser.Parse("", Silence, durationSeconds: 10.0));
        }

        [Fact]
        public void Parse_SilentClipWithManyEqualSamples_DoesNotCountAsClipping()
        {
            // Digital silence has a huge Peak_count (every sample sits at the same level) but no real peak.
            const string silent = """
                lavfi.astats.Overall.Peak_count=264192
                lavfi.astats.Overall.Peak_level=-inf
                lavfi.astats.Overall.Noise_floor=-inf
                """;

            AudioClipStats? stats = AudioStatsParser.Parse(silent, "", durationSeconds: 10.0);

            Assert.NotNull(stats);
            Assert.Equal(0, stats.ClippedPeakCount);
        }

        [Fact]
        public void Parse_PeakAtFullScale_CountsClippedSamples()
        {
            const string clipped = """
                lavfi.astats.Overall.Peak_count=50
                lavfi.astats.Overall.Peak_level=-0.02
                """;

            AudioClipStats? stats = AudioStatsParser.Parse(clipped, "", durationSeconds: 10.0);

            Assert.NotNull(stats);
            Assert.Equal(50, stats.ClippedPeakCount);
        }

        [Fact]
        public void Parse_PeakWellBelowFullScale_DoesNotCountAsClipping()
        {
            const string quiet = """
                lavfi.astats.Overall.Peak_count=2
                lavfi.astats.Overall.Peak_level=-12.0
                """;

            AudioClipStats? stats = AudioStatsParser.Parse(quiet, "", durationSeconds: 10.0);

            Assert.NotNull(stats);
            Assert.Equal(0, stats.ClippedPeakCount);
        }
    }
}
