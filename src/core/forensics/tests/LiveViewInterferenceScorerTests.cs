namespace VideoForensics.Providers.Ring.Forensics.Tests
{
    using VideoForensics.Data.Common.Entities;
    using VideoForensics.Providers.Ring.Implementations;
    using VideoForensics.Providers.Ring.Interfaces;

    public class LiveViewInterferenceScorerTests
    {
        private readonly ILiveViewInterferenceScorer _scorer = new LiveViewInterferenceScorer();

        [Fact]
        public void ScoreSample_BitrateAtBaseline_ReturnsLowScore()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 1000000, // 1 Mbps at baseline
                FractionLost = null,
                JitterTicks = null,
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000, // Same as sample
                StdDevBitrateBps = 100000,
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var score = _scorer.ScoreSample(sample, baseline, null);

            // Assert
            Assert.NotNull(score);
            Assert.True(score < 0.1, "Bitrate at baseline should produce a low score near 0");
        }

        [Fact]
        public void ScoreSample_BitrateAboveBaseline_ReturnsLowScore()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 1500000, // Well above baseline
                FractionLost = null,
                JitterTicks = null,
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 100000,
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var score = _scorer.ScoreSample(sample, baseline, null);

            // Assert
            Assert.NotNull(score);
            Assert.True(score < 0.1, "Bitrate above baseline should not count as degraded");
        }

        [Fact]
        public void ScoreSample_BitrateCollapsed_ReturnsHighScore()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 700000, // 3 std-devs below baseline
                FractionLost = null,
                JitterTicks = null,
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 100000, // 100k std dev
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var score = _scorer.ScoreSample(sample, baseline, null);

            // Assert
            Assert.NotNull(score);
            Assert.True(score > 0.5, "Bitrate several std-devs below baseline should produce high score");
        }

        [Fact]
        public void ScoreSample_NoBucketBaseline_FallsBackToDeviceGlobal()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 700000,
                FractionLost = null,
                JitterTicks = null,
            };

            var deviceGlobalBaseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 0, // Global fallback marker
                IsWeekend = false,
                SampleCount = 500,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 100000,
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var score = _scorer.ScoreSample(sample, null, deviceGlobalBaseline);

            // Assert
            Assert.NotNull(score);
            Assert.True(score > 0.5, "Should score using device global fallback when bucket baseline is null");
        }

        [Fact]
        public void ScoreSample_NoBaselineAtAll_ReturnsNull()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 700000,
                FractionLost = null,
                JitterTicks = null,
            };

            // Act
            var score = _scorer.ScoreSample(sample, null, null);

            // Assert
            Assert.Null(score);
        }

        [Fact]
        public void ScoreSample_NoSampleDataAtAll_ReturnsNull()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = null,
                FractionLost = null,
                JitterTicks = null,
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 100000,
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var score = _scorer.ScoreSample(sample, baseline, null);

            // Assert
            Assert.Null(score);
        }

        [Fact]
        public void ScoreSample_HighLossAndJitterLowBitrate_CombinesIntoHigherScoreThanBitrateAlone()
        {
            // Arrange
            var sampleBitrateOnly = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 700000, // 3 std-devs below
                FractionLost = null,
                JitterTicks = null,
            };

            var sampleCombined = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 700000,
                FractionLost = 5, // 5% loss vs baseline 0%
                JitterTicks = 500, // 500 ticks vs baseline 100
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 100000,
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act
            var scoreBitrateOnly = _scorer.ScoreSample(sampleBitrateOnly, baseline, null);
            var scoreCombined = _scorer.ScoreSample(sampleCombined, baseline, null);

            // Assert
            Assert.NotNull(scoreBitrateOnly);
            Assert.NotNull(scoreCombined);
            Assert.True(scoreCombined > scoreBitrateOnly, "Combined high loss+jitter+low bitrate should score higher than bitrate alone");
        }

        [Fact]
        public void ScoreSample_ZeroBaselineStdDev_DoesNotThrow()
        {
            // Arrange
            var sample = new LiveViewTelemetrySample
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                BitrateBps = 900000, // Below median
                FractionLost = null,
                JitterTicks = null,
            };

            var baseline = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                HourOfDay = 12,
                IsWeekend = false,
                SampleCount = 100,
                MedianBitrateBps = 1000000,
                StdDevBitrateBps = 0, // Zero std dev - edge case
                MedianFractionLost = 0.0,
                MedianJitterTicks = 100,
                LastRecomputedAtUtc = DateTime.UtcNow,
            };

            // Act & Assert - should not throw
            var score = _scorer.ScoreSample(sample, baseline, null);

            Assert.NotNull(score);
            Assert.True(score >= 0 && score <= 1, "Score should be clamped to [0,1]");
        }
    }
}
