using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class RedFlagTests
    {
        [Fact]
        public void Evaluate_BitrateCollapseAndClipping_SuspectsJamming()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: true, ClippedPeakCount: 50, NoiseFloorRiseDb: null, UnusualLighting: false));

            Assert.Contains(RedFlag.BitrateCollapse, report.Flags);
            Assert.Contains(RedFlag.ClippedAudio, report.Flags);
            Assert.True(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_SingleFlag_DoesNotSuspectJamming()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: true, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false));

            Assert.Single(report.Flags);
            Assert.False(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_ThreeFlagsWithoutClipping_SuspectsJamming()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: true, ClippedPeakCount: 0, NoiseFloorRiseDb: 15.0, UnusualLighting: true));

            Assert.Equal(3, report.Flags.Count);
            Assert.True(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_ClippingBelowThreshold_IsNotFlagged()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: RedFlagEvaluator.ClippedPeakThreshold - 1, NoiseFloorRiseDb: null, UnusualLighting: false));

            Assert.DoesNotContain(RedFlag.ClippedAudio, report.Flags);
        }

        [Fact]
        public void Evaluate_NoSignals_HasNoFlags()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: 2.0, UnusualLighting: false));

            Assert.Empty(report.Flags);
            Assert.False(report.JammingSuspected);
        }
    }
}
