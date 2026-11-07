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
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The bit rate histogram: one bar per second, with a hover tooltip giving the bar's clock time and bit rate.</summary>
public class BitrateSeriesChart_Tests : BunitContext
{
    private static readonly DateTime ClipStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    public BitrateSeriesChart_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    private IStringLocalizer<SharedResources> Localizer => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    private IRenderedComponent<BitrateSeriesChart> RenderChart(IReadOnlyList<BitrateSecondDto> points, DateTime? startUtc)
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        return Render<BitrateSeriesChart>(p => p
            .Add(c => c.Points, points)
            .Add(c => c.StartUtc, startUtc)
            .Add(c => c.Player, player.Instance));
    }

    [Fact]
    public void BitrateSeriesChart_Bar_Tooltip_Shows_The_Clock_Time_And_The_Bit_Rate()
    {
        BitrateSecondDto[] points =
        [
            new BitrateSecondDto(0, 1000),
            new BitrateSecondDto(1, 4210),
            new BitrateSecondDto(2, 900),
        ];

        IRenderedComponent<BitrateSeriesChart> cut = RenderChart(points, ClipStart);

        IElement bar = cut.FindAll("[data-testid=bitrate-bar]")[1];
        string time = SignalChartModel.FormatTooltipTime(ClipStart.AddSeconds(1), UserTimeZone.Unavailable);
        string expected = Localizer["BitrateBarTooltip", time, 4210L].Value;
        Assert.Equal(expected, bar.QuerySelector("title")?.TextContent);
    }

    [Fact]
    public void BitrateSeriesChart_Bar_Tooltip_Shows_The_Offset_When_There_Is_No_Start()
    {
        BitrateSecondDto[] points = [new BitrateSecondDto(0, 1000), new BitrateSecondDto(5, 4210)];

        IRenderedComponent<BitrateSeriesChart> cut = RenderChart(points, null);

        IElement bar = cut.FindAll("[data-testid=bitrate-bar]")[1];
        string offset = Localizer["BitrateBarOffset", 5].Value;
        string expected = Localizer["BitrateBarTooltip", offset, 4210L].Value;
        Assert.Equal(expected, bar.QuerySelector("title")?.TextContent);
    }
}
