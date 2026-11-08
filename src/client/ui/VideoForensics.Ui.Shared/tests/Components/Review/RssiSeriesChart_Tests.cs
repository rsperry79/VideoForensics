using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Client.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The RSSI histogram: one bar per reading with a signal in the window, and an empty message when there are none.</summary>
public class RssiSeriesChart_Tests : BunitContext
{
    private static readonly DateTime WindowStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 30, 12, 0, 10, DateTimeKind.Utc);

    public RssiSeriesChart_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    private IRenderedComponent<RssiSeriesChart> RenderChart(IReadOnlyList<SignalSample> readings, double? stdDevDbm = null)
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        return Render<RssiSeriesChart>(p => p
            .Add(c => c.Readings, readings)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.StdDevDbm, stdDevDbm)
            .Add(c => c.Player, player.Instance));
    }

    [Fact]
    public void RssiSeriesChart_Draws_One_Bar_Per_Reading_With_A_Signal()
    {
        SignalSample[] readings = new[]
        {
            new SignalSample(WindowStart.AddSeconds(1), -60),
            new SignalSample(WindowStart.AddSeconds(2), null),
            new SignalSample(WindowStart.AddSeconds(3), -75),
        };

        IRenderedComponent<RssiSeriesChart> cut = RenderChart(readings);

        IReadOnlyList<IElement> bars = cut.FindAll("[data-testid=rssi-bar]");
        Assert.Equal(2, bars.Count);
        Assert.NotNull(cut.Find("[data-testid=rssi-playhead]"));
    }

    [Fact]
    public void RssiSeriesChart_Shows_Empty_Message_When_There_Are_No_Readings()
    {
        IRenderedComponent<RssiSeriesChart> cut = RenderChart([]);

        Assert.Empty(cut.FindAll("[data-testid=rssi-bar]"));
        Assert.NotNull(cut.Find("[data-testid=rssi-series-empty]"));
    }

    [Fact]
    public void RssiSeriesChart_Bar_Tooltip_Shows_The_Clock_Time_And_The_Reading()
    {
        DateTime at = WindowStart.AddSeconds(3);
        SignalSample[] readings = new[] { new SignalSample(at, -75) };

        IRenderedComponent<RssiSeriesChart> cut = RenderChart(readings);

        IElement bar = cut.Find("[data-testid=rssi-bar]");
        string expected = Localizer["RssiBarTooltip", SignalChartModel.FormatTooltipTime(at, UserTimeZone.Unavailable), -75].Value;
        Assert.Equal(expected, bar.QuerySelector("title")?.TextContent);
    }

    [Fact]
    public void RssiSeriesChart_Bar_Tooltip_Adds_The_Standard_Deviation_When_Known()
    {
        DateTime at = WindowStart.AddSeconds(3);
        SignalSample[] readings = new[] { new SignalSample(at, -75) };

        IRenderedComponent<RssiSeriesChart> cut = RenderChart(readings, 4.2);

        IElement bar = cut.Find("[data-testid=rssi-bar]");
        string expected = Localizer["RssiBarTooltipWithStdDev", SignalChartModel.FormatTooltipTime(at, UserTimeZone.Unavailable), -75, "4.2"].Value;
        Assert.Equal(expected, bar.QuerySelector("title")?.TextContent);
    }

    [Fact]
    public void RssiSeriesChart_Bar_Tooltip_Leaves_Out_The_Standard_Deviation_When_Unknown()
    {
        DateTime at = WindowStart.AddSeconds(3);
        SignalSample[] readings = new[] { new SignalSample(at, -75) };

        IRenderedComponent<RssiSeriesChart> cut = RenderChart(readings, null);

        IElement bar = cut.Find("[data-testid=rssi-bar]");
        string expected = Localizer["RssiBarTooltip", SignalChartModel.FormatTooltipTime(at, UserTimeZone.Unavailable), -75].Value;
        Assert.Equal(expected, bar.QuerySelector("title")?.TextContent);
    }

    private IStringLocalizer<SharedResources> Localizer => Services.GetRequiredService<IStringLocalizer<SharedResources>>();
}
