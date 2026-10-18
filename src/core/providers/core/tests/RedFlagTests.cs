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
                BitrateCollapsed: true, ClippedPeakCount: 50, NoiseFloorRiseDb: null, UnusualLighting: false, SilentGapCount: 0, SilenceAboveBaselineFraction: null));

            Assert.Contains(RedFlag.BitrateCollapse, report.Flags);
            Assert.Contains(RedFlag.ClippedAudio, report.Flags);
            Assert.True(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_SingleFlag_DoesNotSuspectJamming()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: true, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false, SilentGapCount: 0, SilenceAboveBaselineFraction: null));

            Assert.Single(report.Flags);
            Assert.False(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_ThreeFlagsWithoutClipping_SuspectsJamming()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: true, ClippedPeakCount: 0, NoiseFloorRiseDb: 15.0, UnusualLighting: true, SilentGapCount: 0, SilenceAboveBaselineFraction: null));

            Assert.Equal(3, report.Flags.Count);
            Assert.True(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_ClippingBelowThreshold_IsNotFlagged()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: RedFlagEvaluator.ClippedPeakThreshold - 1, NoiseFloorRiseDb: null, UnusualLighting: false, SilentGapCount: 0, SilenceAboveBaselineFraction: null));

            Assert.DoesNotContain(RedFlag.ClippedAudio, report.Flags);
        }

        [Fact]
        public void Evaluate_NoSignals_HasNoFlags()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: 2.0, UnusualLighting: false, SilentGapCount: 0, SilenceAboveBaselineFraction: null));

            Assert.Empty(report.Flags);
            Assert.False(report.JammingSuspected);
        }

        [Fact]
        public void Evaluate_RepeatedSilentGapsAboveBaseline_RaisesAudioDropout()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false,
                SilentGapCount: 5, SilenceAboveBaselineFraction: 0.2));

            Assert.Contains(RedFlag.AudioDropout, report.Flags);
        }

        [Fact]
        public void Evaluate_SilentGapsWithoutBaseline_NotFlagged()
        {
            // Without a baseline a silent camera microphone cannot be told from a dropout.
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false,
                SilentGapCount: 5, SilenceAboveBaselineFraction: null));

            Assert.DoesNotContain(RedFlag.AudioDropout, report.Flags);
        }

        [Fact]
        public void Evaluate_SilenceWithinBaseline_NotFlagged()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false,
                SilentGapCount: 5, SilenceAboveBaselineFraction: 0.02));

            Assert.DoesNotContain(RedFlag.AudioDropout, report.Flags);
        }

        [Fact]
        public void Evaluate_OneSilentGap_NotFlagged()
        {
            RedFlagReport report = RedFlagEvaluator.Evaluate(new ClipSignals(
                BitrateCollapsed: false, ClippedPeakCount: 0, NoiseFloorRiseDb: null, UnusualLighting: false,
                SilentGapCount: 1, SilenceAboveBaselineFraction: 0.5));

            Assert.DoesNotContain(RedFlag.AudioDropout, report.Flags);
        }
    }
}
