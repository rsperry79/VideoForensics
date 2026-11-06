using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The histogram timeline: the scrub timeline sits above, and any histogram is fed in below it.</summary>
public class HistogramTimeline_Tests : BunitContext
{
    private static readonly DateTime WindowStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc);

    public HistogramTimeline_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    private IRenderedComponent<HistogramTimeline> RenderControl(RenderFragment? histogram)
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        return Render<HistogramTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.Histogram, histogram));
    }

    [Fact]
    public void HistogramTimeline_Places_Histogram_Below_ScrubTimeline()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderControl(builder => builder.AddMarkupContent(0, "<div data-testid=\"fed-histogram\"></div>"));

        string markup = cut.Markup;
        int timelineAt = markup.IndexOf("data-testid=\"playback-timeline\"", StringComparison.Ordinal);
        int histogramAt = markup.IndexOf("data-testid=\"fed-histogram\"", StringComparison.Ordinal);

        Assert.True(timelineAt >= 0, "The scrub timeline should render.");
        Assert.True(histogramAt > timelineAt, "The histogram should render below the scrub timeline.");
    }

    [Fact]
    public void HistogramTimeline_Renders_Fed_Histogram_Content_In_Its_Slot()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderControl(builder => builder.AddMarkupContent(0, "<div data-testid=\"fed-histogram\"></div>"));

        IElement slot = cut.Find("[data-testid=histogram-timeline-histogram]");
        Assert.NotNull(slot.QuerySelector("[data-testid=fed-histogram]"));
    }

    [Fact]
    public void HistogramTimeline_Renders_Scrub_Timeline_Without_Histogram()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderControl(histogram: null);

        Assert.NotNull(cut.Find("[data-testid=playback-timeline]"));
        Assert.Empty(cut.FindAll("[data-testid=fed-histogram]"));
    }
}
