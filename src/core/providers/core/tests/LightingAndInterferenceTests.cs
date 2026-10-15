using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class LightingAndInterferenceTests
    {
        [Fact]
        public void ParseMeanSaturation_PerFrameSignalstats_ReturnsAverage()
        {
            const string output = """
                frame:0    pts:0       pts_time:0
                lavfi.signalstats.SATAVG=10.000000
                frame:1    pts:1       pts_time:1
                lavfi.signalstats.SATAVG=20.000000
                """;

            Assert.Equal(15.0, SaturationAnalysis.ParseMeanSaturation(output));
        }

        [Fact]
        public void ParseMeanSaturation_NoSignalstatsLines_ReturnsNull()
        {
            Assert.Null(SaturationAnalysis.ParseMeanSaturation("frame:0 pts:0"));
        }

        [Theory]
        [InlineData(0.4, LightingMode.Ir)]
        [InlineData(7.9, LightingMode.Ir)]
        [InlineData(8.0, LightingMode.Day)]
        [InlineData(45.0, LightingMode.Day)]
        public void Classify_MeanSaturation_LabelsIrGrayscaleAndColourDay(double saturation, LightingMode expected)
        {
            Assert.Equal(expected, LightingClassifier.Classify(saturation));
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
    }
}
