using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class LightingAndInterferenceTests
    {
        [Fact]
        public void ParseStats_PerFrameSignalstats_ReturnsAveragedSaturationAndLuma()
        {
            const string output = """
                frame:0    pts:0       pts_time:0
                lavfi.signalstats.YAVG=40.000000
                lavfi.signalstats.SATAVG=10.000000
                frame:1    pts:1       pts_time:1
                lavfi.signalstats.YAVG=60.000000
                lavfi.signalstats.SATAVG=20.000000
                """;

            ClipColorStats? stats = SaturationAnalysis.ParseStats(output);

            Assert.NotNull(stats);
            Assert.Equal(15.0, stats.MeanSaturation);
            Assert.Equal(50.0, stats.MeanLuma);
        }

        [Fact]
        public void ParseStats_NoSignalstatsLines_ReturnsNull()
        {
            Assert.Null(SaturationAnalysis.ParseStats("frame:0 pts:0"));
        }

        [Theory]
        [InlineData(0.4, 20.0, LightingMode.Ir)]        // grayscale and dark: infrared
        [InlineData(7.9, 30.0, LightingMode.Ir)]
        [InlineData(45.0, 25.0, LightingMode.ColorNight)] // colour kept but dark: floodlit colour night
        [InlineData(45.0, 150.0, LightingMode.Day)]     // colour and bright: day or HDR
        [InlineData(0.4, 150.0, LightingMode.Day)]      // grayscale but bright is not IR
        public void Classify_SaturationAndLuma_LabelsIrColorNightAndDay(double saturation, double luma, LightingMode expected)
        {
            Assert.Equal(expected, LightingClassifier.Classify(new ClipColorStats(saturation, luma)));
        }

        [Fact]
        public void Assess_BitrateWellBelowSameModeBaseline_FlagsSuspectedInterference()
        {
            var baseline = new BitrateBaseline(MedianBps: 1_000_000, SampleCount: 20);

            InterferenceAssessment result = InterferenceAssessor.Assess(400_000, baseline);

            Assert.True(result.InterferenceSuspected);
            Assert.Equal(1_000_000, result.BaselineBps);
        }

        [Fact]
        public void Assess_BitrateNearBaseline_NotFlagged()
        {
            var baseline = new BitrateBaseline(MedianBps: 1_000_000, SampleCount: 20);

            Assert.False(InterferenceAssessor.Assess(900_000, baseline).InterferenceSuspected);
        }

        [Fact]
        public void Assess_TooFewSamplesForBaseline_NotFlagged()
        {
            // A baseline from a handful of clips is not trustworthy enough to call a drop interference.
            var baseline = new BitrateBaseline(MedianBps: 1_000_000, SampleCount: 3);

            Assert.False(InterferenceAssessor.Assess(100_000, baseline).InterferenceSuspected);
        }

        [Fact]
        public void Assess_NoBaseline_NotFlagged()
        {
            InterferenceAssessment result = InterferenceAssessor.Assess(100_000, baseline: null);

            Assert.False(result.InterferenceSuspected);
            Assert.Null(result.BaselineBps);
        }

        [Fact]
        public void BitrateBaseline_From_ComputesMedianAndCount()
        {
            BitrateBaseline? baseline = BitrateBaseline.From(new long[] { 900_000, 1_000_000, 1_200_000, 5_000_000 });

            Assert.NotNull(baseline);
            Assert.Equal(1_100_000, baseline.MedianBps); // median of the four: (1.0M + 1.2M) / 2
            Assert.Equal(4, baseline.SampleCount);
        }

        [Fact]
        public void BitrateBaseline_From_NoSamples_ReturnsNull()
        {
            Assert.Null(BitrateBaseline.From(Array.Empty<long>()));
        }
    }
}
