using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class DriftAndHemisphereTests
    {
        [Theory]
        [InlineData(1, Season.Summer)]
        [InlineData(7, Season.Winter)]
        [InlineData(10, Season.Spring)]
        [InlineData(4, Season.Autumn)]
        public void FromMonth_SouthernHemisphere_IsOffsetBySixMonths(int month, Season expected)
        {
            Assert.Equal(expected, SeasonExtensions.FromMonth(month, Hemisphere.Southern));
        }

        [Fact]
        public void Detect_SteadyRiseOverManyClips_ReportsDrift()
        {
            // Median falls from 1.0 Mb/s to 0.6 Mb/s across the history: a 40% move, well above the threshold.
            IReadOnlyList<long> bitrates = Enumerable.Range(0, 60)
                .Select(i => (long)(1_000_000 - i * (400_000 / 60.0)))
                .ToList();

            DriftReport report = DriftDetector.Detect(bitrates);

            Assert.True(report.Drifting);
            Assert.True(report.ChangeFraction > DriftDetector.DriftFraction);
        }

        [Fact]
        public void Detect_StableHistory_ReportsNoDrift()
        {
            IReadOnlyList<long> bitrates = Enumerable.Repeat(1_000_000L, 60).ToList();

            Assert.False(DriftDetector.Detect(bitrates).Drifting);
        }

        [Fact]
        public void Detect_TooFewClips_ReportsNoDrift()
        {
            IReadOnlyList<long> bitrates = Enumerable.Range(0, 10).Select(i => (long)(1_000_000 + i * 100_000)).ToList();

            Assert.False(DriftDetector.Detect(bitrates).Drifting);
        }
    }
}
