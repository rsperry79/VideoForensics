using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>Per-second bitrate from video packet sizes: covers the whole clip, buckets by packet time, and keeps empty seconds at zero.</summary>
    public class BitrateSeriesTests
    {
        [Fact]
        public void BitrateSeries_FromPackets_SumsBytesPerSecond()
        {
            IReadOnlyList<BitrateSecond> series = BitrateSeries.FromPackets(
            [
                (0.0, 1000L), (0.5, 1000L), (1.2, 500L),
            ]);

            Assert.Equal(2, series.Count);
            Assert.Equal(16, series[0].Kbps);
            Assert.Equal(4, series[1].Kbps);
        }

        [Fact]
        public void BitrateSeries_FromPackets_GapSecondIsZero()
        {
            IReadOnlyList<BitrateSecond> series = BitrateSeries.FromPackets(
            [
                (0.1, 1000L), (2.1, 1000L),
            ]);

            Assert.Equal(3, series.Count);
            Assert.Equal(0, series[1].Kbps);
            Assert.Equal(1, series[1].Second);
        }

        [Fact]
        public void BitrateSeries_FromPackets_CoversWholeClip()
        {
            IReadOnlyList<BitrateSecond> series = BitrateSeries.FromPackets(
            [
                (0.0, 1000L), (59.9, 1000L),
            ]);

            Assert.Equal(60, series.Count);
            Assert.Equal(59, series[^1].Second);
        }

        [Fact]
        public void BitrateSeries_FromPackets_Empty_ReturnsEmpty()
        {
            Assert.Empty(BitrateSeries.FromPackets([]));
        }

        [Fact]
        public void BitrateSeries_FromPackets_NegativeTime_IsSkipped()
        {
            IReadOnlyList<BitrateSecond> series = BitrateSeries.FromPackets(
            [
                (-1.0, 1000L), (0.5, 1000L),
            ]);

            Assert.Single(series);
            Assert.Equal(8, series[0].Kbps);
        }
    }
}
