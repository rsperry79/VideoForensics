using System.Globalization;

using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests.Services
{
    public class LiveViewTelemetryFormatterTests
    {
        [Fact]
        public void Score_Null_ReturnsNotAvailableKey()
        {
            Assert.Equal(LiveViewTelemetryFormatter.NotAvailableKey, LiveViewTelemetryFormatter.FormatScore(null));
        }

        [Fact]
        public void Score_Value_FormatsTwoDecimalsInvariant()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                // A comma-decimal culture must not change the output: the numeric text is always invariant.
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                Assert.Equal("0.40", LiveViewTelemetryFormatter.FormatScore(0.4));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void LossPercent_Value_ScalesFractionLostToOneDecimal()
        {
            // RTCP fraction lost is 0-255; 255 is 100 percent.
            Assert.Equal("100.0", LiveViewTelemetryFormatter.FormatLossPercent(255));
            Assert.Equal("50.2", LiveViewTelemetryFormatter.FormatLossPercent(128));
        }

        [Fact]
        public void LossPercent_Null_ReturnsNotAvailableKey()
        {
            Assert.Equal(LiveViewTelemetryFormatter.NotAvailableKey, LiveViewTelemetryFormatter.FormatLossPercent(null));
        }

        [Fact]
        public void Kilobits_Value_DividesByThousandInvariant()
        {
            Assert.Equal("2500", LiveViewTelemetryFormatter.FormatKilobitsPerSecond(2_500_000L));
        }

        [Fact]
        public void Count_Null_ReturnsNotAvailableKey()
        {
            Assert.Equal(LiveViewTelemetryFormatter.NotAvailableKey, LiveViewTelemetryFormatter.FormatCount(null));
        }
    }
}
