
using Bunit;

using VideoForensics.Ui.Shared.Components;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// The snapshot interval is stored in seconds. The input shows it in minutes when it is a whole number of minutes,
/// lets the operator switch the unit, and always reports the value back in seconds.
/// </summary>
public class IntervalSecondsInputTests : AccountDetailsPageTestBase
{
    [Fact]
    public void OneMinuteDefault_ShowsOneWithMinutesSelected()
    {
        IRenderedComponent<IntervalSecondsInput> cut = Render<IntervalSecondsInput>(p => p
            .Add(c => c.Id, "snapshot-rssi-interval")
            .Add(c => c.Value, 60));

        Assert.Equal("1", cut.Find("input").GetAttribute("value"));
        Assert.Contains("selected", cut.Find("option[value='Minutes']").OuterHtml);
    }

    [Fact]
    public void SwitchingToSeconds_ThenEntering30_ReportsThirtySeconds()
    {
        int reported = -1;
        IRenderedComponent<IntervalSecondsInput> cut = Render<IntervalSecondsInput>(p => p
            .Add(c => c.Id, "snapshot-rssi-interval")
            .Add(c => c.Value, 60)
            .Add(c => c.ValueChanged, v => reported = v));

        cut.Find("select").Change("Seconds");
        cut.Find("input").Change("30");

        Assert.Equal(30, reported);
    }

    [Fact]
    public void MinutesUnit_EnteringTwo_ReportsOneHundredTwenty()
    {
        int reported = -1;
        IRenderedComponent<IntervalSecondsInput> cut = Render<IntervalSecondsInput>(p => p
            .Add(c => c.Id, "snapshot-rssi-interval")
            .Add(c => c.Value, 60)
            .Add(c => c.ValueChanged, v => reported = v));

        cut.Find("input").Change("2");

        Assert.Equal(120, reported);
    }

    [Fact]
    public void NonWholeMinuteValue_OpensInSeconds()
    {
        IRenderedComponent<IntervalSecondsInput> cut = Render<IntervalSecondsInput>(p => p
            .Add(c => c.Id, "snapshot-rssi-interval")
            .Add(c => c.Value, 45));

        Assert.Equal("45", cut.Find("input").GetAttribute("value"));
        Assert.Contains("selected", cut.Find("option[value='Seconds']").OuterHtml);
    }
}
