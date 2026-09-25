namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Ui.Shared.Services.Evidence;

public class RssiPlot_Tests
{
    private static readonly DateTime RangeStart = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RangeEnd = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc); // 10 hours

    [Fact]
    public void BuildPolylinePoints_NoSamples_ReturnsEmptyString()
    {
        var result = RssiPlot.BuildPolylinePoints(Array.Empty<RssiSample>(), RangeStart, RangeEnd, 100, 50);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildPolylinePoints_RangeEndNotAfterStart_ReturnsEmptyString()
    {
        var samples = new[] { new RssiSample(RangeStart, -60) };

        var result = RssiPlot.BuildPolylinePoints(samples, RangeStart, RangeStart, 100, 50);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildPolylinePoints_SingleSampleAtStart_PlotsAtLeftEdge()
    {
        var samples = new[] { new RssiSample(RangeStart, -65) }; // midpoint of -100..-30 range

        var result = RssiPlot.BuildPolylinePoints(samples, RangeStart, RangeEnd, 100, 50, minRssi: -100, maxRssi: -30);

        var parts = result.Split(' ');
        Assert.Single(parts);
        var xy = parts[0].Split(',');
        Assert.Equal(0.0, double.Parse(xy[0]), 2);
        Assert.Equal(25.0, double.Parse(xy[1]), 2); // -65 is the midpoint -> mid-height
    }

    [Fact]
    public void BuildPolylinePoints_SampleAtRangeMidpoint_PlotsAtHorizontalCenter()
    {
        var midTime = RangeStart.AddHours(5);
        var samples = new[] { new RssiSample(midTime, -30) }; // strongest -> top (y=0)

        var result = RssiPlot.BuildPolylinePoints(samples, RangeStart, RangeEnd, 100, 50, minRssi: -100, maxRssi: -30);

        var xy = result.Split(',');
        Assert.Equal(50.0, double.Parse(xy[0]), 2);
        Assert.Equal(0.0, double.Parse(xy[1]), 2);
    }

    [Fact]
    public void BuildPolylinePoints_WeakestSignal_PlotsAtBottom()
    {
        var samples = new[] { new RssiSample(RangeStart, -100) };

        var result = RssiPlot.BuildPolylinePoints(samples, RangeStart, RangeEnd, 100, 50, minRssi: -100, maxRssi: -30);

        var xy = result.Split(',');
        Assert.Equal(50.0, double.Parse(xy[1]), 2); // bottom of the plot
    }

    [Fact]
    public void BuildPolylinePoints_RssiOutsideConfiguredRange_IsClamped()
    {
        var strongerThanMax = new[] { new RssiSample(RangeStart, -10) };
        var weakerThanMin = new[] { new RssiSample(RangeStart, -120) };

        var strongResult = RssiPlot.BuildPolylinePoints(strongerThanMax, RangeStart, RangeEnd, 100, 50, minRssi: -100, maxRssi: -30);
        var weakResult = RssiPlot.BuildPolylinePoints(weakerThanMin, RangeStart, RangeEnd, 100, 50, minRssi: -100, maxRssi: -30);

        Assert.Equal(0.0, double.Parse(strongResult.Split(',')[1]), 2);
        Assert.Equal(50.0, double.Parse(weakResult.Split(',')[1]), 2);
    }

    [Fact]
    public void BuildPolylinePoints_MultipleSamples_OrderedByTimestampRegardlessOfInputOrder()
    {
        var early = new RssiSample(RangeStart.AddHours(1), -60);
        var late = new RssiSample(RangeStart.AddHours(8), -60);

        var result = RssiPlot.BuildPolylinePoints(new[] { late, early }, RangeStart, RangeEnd, 100, 50);

        var points = result.Split(' ');
        var firstX = double.Parse(points[0].Split(',')[0]);
        var secondX = double.Parse(points[1].Split(',')[0]);
        Assert.True(firstX < secondX);
    }

    [Fact]
    public void BuildPolylinePoints_SampleOutsideTimeRange_IsClampedToNearestEdge()
    {
        var beforeRange = new RssiSample(RangeStart.AddHours(-2), -60);
        var afterRange = new RssiSample(RangeEnd.AddHours(2), -60);

        var result = RssiPlot.BuildPolylinePoints(new[] { beforeRange, afterRange }, RangeStart, RangeEnd, 100, 50);

        var points = result.Split(' ');
        Assert.Equal(0.0, double.Parse(points[0].Split(',')[0]), 2);
        Assert.Equal(100.0, double.Parse(points[1].Split(',')[0]), 2);
    }

    [Fact]
    public void MarkerX_AtRangeStart_ReturnsZero()
    {
        var x = RssiPlot.MarkerX(RangeStart, RangeStart, RangeEnd, 100);

        Assert.Equal(0.0, x, 2);
    }

    [Fact]
    public void MarkerX_AtRangeEnd_ReturnsWidth()
    {
        var x = RssiPlot.MarkerX(RangeEnd, RangeStart, RangeEnd, 100);

        Assert.Equal(100.0, x, 2);
    }

    [Fact]
    public void MarkerX_AtMidpoint_ReturnsHalfWidth()
    {
        var x = RssiPlot.MarkerX(RangeStart.AddHours(5), RangeStart, RangeEnd, 100);

        Assert.Equal(50.0, x, 2);
    }

    [Fact]
    public void MarkerX_ZeroWidthRange_ReturnsZero()
    {
        var x = RssiPlot.MarkerX(RangeStart, RangeStart, RangeStart, 100);

        Assert.Equal(0.0, x, 2);
    }

    [Fact]
    public void MarkerX_TimestampOutsideRange_IsClamped()
    {
        var beforeX = RssiPlot.MarkerX(RangeStart.AddHours(-5), RangeStart, RangeEnd, 100);
        var afterX = RssiPlot.MarkerX(RangeEnd.AddHours(5), RangeStart, RangeEnd, 100);

        Assert.Equal(0.0, beforeX, 2);
        Assert.Equal(100.0, afterX, 2);
    }
}
