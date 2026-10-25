using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Signal chart geometry: readings map onto the plot, selections map back to time, and axis ticks are evenly spaced.</summary>
public class SignalChartModelTests
{
    private static readonly DateTime From = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MapX_PlacesTheWindowEdgesOnThePlotEdges()
    {
        Assert.Equal(SignalChartModel.PlotLeft, SignalChartModel.MapX(From, From, To), precision: 6);
        Assert.Equal(SignalChartModel.PlotLeft + SignalChartModel.PlotWidth, SignalChartModel.MapX(To, From, To), precision: 6);
    }

    [Fact]
    public void MapDbm_PutsStrongerSignalHigher()
    {
        double strong = SignalChartModel.MapDbm(-30);
        double weak = SignalChartModel.MapDbm(-100);

        Assert.Equal(SignalChartModel.PlotTop, strong, precision: 6);
        Assert.Equal(SignalChartModel.PlotTop + SignalChartModel.PlotHeight, weak, precision: 6);
        Assert.True(strong < weak);
    }

    [Fact]
    public void MapDbm_ClampsValuesOutsideTheAxis()
    {
        Assert.Equal(SignalChartModel.MapDbm(-100), SignalChartModel.MapDbm(-150), precision: 6);
        Assert.Equal(SignalChartModel.MapDbm(-30), SignalChartModel.MapDbm(-5), precision: 6);
    }

    [Fact]
    public void Polyline_SkipsReadingsWithoutSignalAndOutsideTheRange()
    {
        var samples = new[]
        {
            new SignalSample(From.AddHours(1), -50),
            new SignalSample(From.AddHours(2), null),
            new SignalSample(To.AddHours(1), -60),
            new SignalSample(From.AddHours(3), -70),
        };

        string points = SignalChartModel.Polyline(samples, From, To);

        // Two plotted points, in time order.
        Assert.Equal(2, points.Split(' ').Length);
        Assert.StartsWith($"{SignalChartModel.MapX(From.AddHours(1), From, To):0.#},", points);
    }

    [Fact]
    public void TimeTicks_AreEvenlySpacedAcrossTheWindow()
    {
        IReadOnlyList<DateTime> ticks = SignalChartModel.TimeTicks(From, To, 5);

        Assert.Equal(5, ticks.Count);
        Assert.Equal(From, ticks[0]);
        Assert.Equal(To, ticks[^1]);
        Assert.Equal(TimeSpan.FromHours(24), ticks[1] - ticks[0]);
    }

    [Fact]
    public void SelectionRange_ConvertsFractionsToTimeInEitherDragDirection()
    {
        (DateTime start, DateTime end)? forward = SignalChartModel.SelectionRange(0.25, 0.5, From, To);
        (DateTime start, DateTime end)? backward = SignalChartModel.SelectionRange(0.5, 0.25, From, To);

        Assert.NotNull(forward);
        Assert.Equal(From.AddHours(24), forward!.Value.start);
        Assert.Equal(From.AddHours(48), forward.Value.end);
        Assert.Equal(forward, backward);
    }

    [Fact]
    public void SelectionRange_RejectsAZeroWidthSelection()
    {
        Assert.Null(SignalChartModel.SelectionRange(0.4, 0.4, From, To));
    }

    [Fact]
    public void FractionFromPixel_ClampsToThePlotWidth()
    {
        Assert.Equal(0.5, SignalChartModel.FractionFromPixel(200, 400), precision: 6);
        Assert.Equal(0, SignalChartModel.FractionFromPixel(-10, 400));
        Assert.Equal(1, SignalChartModel.FractionFromPixel(900, 400));
        Assert.Equal(0, SignalChartModel.FractionFromPixel(10, 0));
    }

    [Fact]
    public void FractionFromCanvasOffset_SubtractsTheLeftMarginAtAnyWidth()
    {
        // At 2x the view box size, the plot starts at 2 * PlotLeft pixels.
        double canvas = SignalChartModel.ViewWidth * 2;
        double plotStart = SignalChartModel.PlotLeft * 2;

        Assert.Equal(0, SignalChartModel.FractionFromCanvasOffset(plotStart, canvas), precision: 6);
        Assert.Equal(1, SignalChartModel.FractionFromCanvasOffset(plotStart + SignalChartModel.PlotWidth * 2, canvas), precision: 6);
        Assert.Equal(0, SignalChartModel.FractionFromCanvasOffset(10, canvas));
    }

    [Fact]
    public void ExpectedIntervalFor_UsesTheAccountsSnapshotIntervalAndFallsBackToTheDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), SignalChartModel.ExpectedIntervalFor(120));
        Assert.Equal(TimeSpan.FromSeconds(60), SignalChartModel.ExpectedIntervalFor(null));
    }

    [Fact]
    public void FormatTick_UsesTimeOnlyForShortWindowsAndDateForLongOnes()
    {
        var noon = new DateTime(2026, 10, 24, 12, 30, 0, DateTimeKind.Utc);

        Assert.Equal("12:30", SignalChartModel.FormatTick(noon, TimeSpan.FromHours(6)));
        Assert.Equal("Oct 24", SignalChartModel.FormatTick(noon, TimeSpan.FromDays(30)));
    }
}
