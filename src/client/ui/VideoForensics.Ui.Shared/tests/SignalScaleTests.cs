using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The y-axis fits the displayed readings with padding, so the values can be read.</summary>
public class SignalScaleTests
{
    [Fact]
    public void Fit_PadsFifteenPercentOfTheDisplayedDeltaOnEachSide()
    {
        // Delta is 20 dBm, so 3 dBm of padding on each side.
        SignalScale scale = SignalScale.Fit(min: -70, max: -50);

        Assert.Equal(-73, scale.WeakestDbm, precision: 6);
        Assert.Equal(-47, scale.StrongestDbm, precision: 6);
    }

    [Fact]
    public void Fit_ExpandsAFlatReadingToTheMinimumSpan()
    {
        SignalScale scale = SignalScale.Fit(min: -60, max: -60);

        Assert.Equal(-65, scale.WeakestDbm, precision: 6);
        Assert.Equal(-55, scale.StrongestDbm, precision: 6);
    }

    [Fact]
    public void MapY_PutsTheStrongestEdgeAtTheTopAndTheWeakestAtTheBottom()
    {
        SignalScale scale = SignalScale.Fit(min: -70, max: -50);

        Assert.Equal(SignalChartModel.PlotTop, scale.MapY(scale.StrongestDbm), precision: 6);
        Assert.Equal(SignalChartModel.PlotTop + SignalChartModel.PlotHeight, scale.MapY(scale.WeakestDbm), precision: 6);
    }

    [Fact]
    public void MapY_ClampsValuesOutsideTheScale()
    {
        SignalScale scale = SignalScale.Fit(min: -70, max: -50);

        Assert.Equal(scale.MapY(scale.WeakestDbm), scale.MapY(-120), precision: 6);
        Assert.Equal(scale.MapY(scale.StrongestDbm), scale.MapY(0), precision: 6);
    }

    [Fact]
    public void Ticks_AreRoundNumbersInsideTheScale()
    {
        // Span 26 dBm: a 5 dBm step gives about five labels.
        SignalScale scale = SignalScale.Fit(min: -70, max: -50);

        IReadOnlyList<int> ticks = scale.Ticks();

        Assert.Equal([-70, -65, -60, -55, -50], ticks);
    }

    [Fact]
    public void Default_CoversTheFullRangeAnEmptyChartNeeds()
    {
        SignalScale scale = SignalScale.Default;

        Assert.Equal(-100, scale.WeakestDbm, precision: 6);
        Assert.Equal(-30, scale.StrongestDbm, precision: 6);
    }
}
