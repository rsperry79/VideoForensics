using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The bit rate chart's y-axis fits the displayed kbps readings and picks readable gridlines at any magnitude.</summary>
public class BitrateScaleTests
{
    [Fact]
    public void Fit_PadsFifteenPercentOfTheDisplayedDeltaOnEachSide()
    {
        // Delta is 2000 kbps, so 300 kbps of padding on each side.
        var scale = BitrateScale.Fit(min: 3000, max: 5000);

        Assert.Equal(2700, scale.LowKbps, precision: 6);
        Assert.Equal(5300, scale.HighKbps, precision: 6);
    }

    [Fact]
    public void Fit_ExpandsAFlatReadingToTheMinimumSpan()
    {
        var scale = BitrateScale.Fit(min: 500, max: 500);

        Assert.Equal(495, scale.LowKbps, precision: 6);
        Assert.Equal(505, scale.HighKbps, precision: 6);
    }

    [Fact]
    public void Fit_NeverPutsTheBottomBelowZero()
    {
        var scale = BitrateScale.Fit(min: 2, max: 2);

        Assert.Equal(0, scale.LowKbps, precision: 6);
        Assert.True(scale.HighKbps - scale.LowKbps >= BitrateScale.MinimumSpanKbps);
    }

    [Fact]
    public void Default_CoversZeroToTenThousandKbps()
    {
        Assert.Equal(0, BitrateScale.Default.LowKbps);
        Assert.Equal(10000, BitrateScale.Default.HighKbps);
    }

    [Fact]
    public void MapY_PutsTheHighestEdgeAtTheTopAndTheLowestAtTheBottom()
    {
        var scale = BitrateScale.Fit(min: 3000, max: 5000);

        Assert.Equal(SignalChartModel.PlotTop, scale.MapY(scale.HighKbps), precision: 6);
        Assert.Equal(SignalChartModel.PlotTop + SignalChartModel.PlotHeight, scale.MapY(scale.LowKbps), precision: 6);
        Assert.True(scale.MapY(4500) < scale.MapY(3500));
    }

    [Fact]
    public void MapY_ClampsValuesOutsideTheScale()
    {
        var scale = BitrateScale.Fit(min: 3000, max: 5000);

        Assert.Equal(SignalChartModel.PlotTop, scale.MapY(1_000_000), precision: 6);
        Assert.Equal(SignalChartModel.PlotTop + SignalChartModel.PlotHeight, scale.MapY(-5), precision: 6);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(500, 500)]
    [InlineData(10, 60)]
    [InlineData(1000, 4000)]
    [InlineData(0, 10000)]
    [InlineData(100, 50000)]
    public void Ticks_AreFewNiceAndInsideTheScale_AcrossSpans(int min, int max)
    {
        var scale = BitrateScale.Fit(min, max);

        IReadOnlyList<int> ticks = scale.Ticks();

        Assert.InRange(ticks.Count, 2, 8);
        Assert.All(ticks, t => Assert.InRange(t, scale.LowKbps, scale.HighKbps));
        int step = ticks[1] - ticks[0];
        Assert.True(step >= 1);
        Assert.All(ticks.Zip(ticks.Skip(1)), pair => Assert.Equal(step, pair.Second - pair.First));

        // A nice step is 1, 2 or 5 times a power of ten.
        int mantissa = step;
        while (mantissa >= 10 && mantissa % 10 == 0)
        {
            mantissa /= 10;
        }

        Assert.Contains(mantissa, new[] { 1, 2, 5 });
    }

    [Fact]
    public void Ticks_DefaultScale_StepsByTwoThousand()
    {
        Assert.Equal(new[] { 0, 2000, 4000, 6000, 8000, 10000 }, BitrateScale.Default.Ticks());
    }

    [Fact]
    public void Polyline_UsesTheBitrateScale_AndSkipsReadingsOutsideTheWindow()
    {
        var from = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(10);
        var scale = BitrateScale.Fit(1000, 2000);
        SignalSample[] samples = new[]
        {
            new SignalSample(from.AddHours(1), 1000),
            new SignalSample(from.AddHours(2), 2000),
            new SignalSample(from.AddHours(20), 1500),
        };

        string points = SignalChartModel.Polyline(samples, from, to, scale.MapY);

        Assert.Equal(2, points.Split(' ').Length);
    }
}
