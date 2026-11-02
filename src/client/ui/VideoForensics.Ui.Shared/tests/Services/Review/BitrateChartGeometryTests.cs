using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Review
{
    /// <summary>The bitrate chart maps kbps and playback time to fractions of the chart size, clamped to the chart.</summary>
    public class BitrateChartGeometryTests
    {
        [Fact]
        public void BitrateChartGeometry_BarFraction_ScalesToTheMaximum()
        {
            Assert.Equal(0.5, BitrateChartGeometry.BarFraction(50, 100));
        }

        [Fact]
        public void BitrateChartGeometry_BarFraction_NoMaximum_IsZero()
        {
            Assert.Equal(0, BitrateChartGeometry.BarFraction(50, 0));
        }

        [Fact]
        public void BitrateChartGeometry_BarFraction_AboveMaximum_IsClampedToOne()
        {
            Assert.Equal(1, BitrateChartGeometry.BarFraction(200, 100));
        }

        [Fact]
        public void BitrateChartGeometry_PlayheadFraction_IsHalfwayThroughAHalfMinuteOfSixtySeconds()
        {
            Assert.Equal(0.5, BitrateChartGeometry.PlayheadFraction(30, 60));
        }

        [Fact]
        public void BitrateChartGeometry_PlayheadFraction_BeforeStart_IsZero()
        {
            Assert.Equal(0, BitrateChartGeometry.PlayheadFraction(-5, 60));
        }

        [Fact]
        public void BitrateChartGeometry_PlayheadFraction_NoSeconds_IsZero()
        {
            Assert.Equal(0, BitrateChartGeometry.PlayheadFraction(10, 0));
        }
    }
}
