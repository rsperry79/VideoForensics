using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class SeasonalBaselineTests
    {
        private static BaselineSample Sample(long bitrate, LightingMode mode, Season season, bool flagged = false) =>
            new(bitrate, mode, season, flagged);

        [Fact]
        public void Build_FlaggedClips_AreExcludedFromBaseline()
        {
            var samples = Enumerable.Range(0, 10).Select(_ => Sample(1_000_000, LightingMode.Day, Season.Summer))
                .Concat(Enumerable.Range(0, 10).Select(_ => Sample(100_000, LightingMode.Day, Season.Summer, flagged: true)))
                .ToList();

            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(1, samples);

            Assert.Equal(1_000_000, snapshot.Get(LightingMode.Day, Season.Summer)!.MedianBps);
        }

        [Fact]
        public void Build_SeasonsAreBucketedSeparately()
        {
            var samples = Enumerable.Range(0, 10).Select(_ => Sample(1_400_000, LightingMode.Day, Season.Summer))
                .Concat(Enumerable.Range(0, 10).Select(_ => Sample(800_000, LightingMode.Day, Season.Winter)))
                .ToList();

            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(1, samples);

            Assert.Equal(1_400_000, snapshot.Get(LightingMode.Day, Season.Summer)!.MedianBps);
            Assert.Equal(800_000, snapshot.Get(LightingMode.Day, Season.Winter)!.MedianBps);
        }

        [Fact]
        public void Build_SparseSeason_FallsBackToModeWideBaseline()
        {
            // Only three winter clips: not enough for a winter bucket, so winter gets the all-season baseline for the mode.
            var samples = Enumerable.Range(0, 10).Select(_ => Sample(1_000_000, LightingMode.Ir, Season.Summer))
                .Concat(Enumerable.Range(0, 3).Select(_ => Sample(900_000, LightingMode.Ir, Season.Winter)))
                .ToList();

            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(1, samples);

            BitrateBaseline? winter = snapshot.Get(LightingMode.Ir, Season.Winter);
            Assert.NotNull(winter);
            Assert.Equal(1_000_000, winter.MedianBps);
        }

        [Fact]
        public void Build_TooFewSamplesOverall_HasNoBaseline()
        {
            BaselineSnapshot snapshot = SeasonalBaselineBuilder.Build(1, new[] { Sample(1_000_000, LightingMode.Day, Season.Summer) });

            Assert.Null(snapshot.Get(LightingMode.Day, Season.Summer));
        }

        [Theory]
        [InlineData(12, Season.Winter)]
        [InlineData(1, Season.Winter)]
        [InlineData(4, Season.Spring)]
        [InlineData(7, Season.Summer)]
        [InlineData(10, Season.Autumn)]
        public void Season_FromMonth_MapsNorthernHemisphereMonths(int month, Season expected)
        {
            Assert.Equal(expected, SeasonExtensions.FromMonth(month));
        }
    }
}
