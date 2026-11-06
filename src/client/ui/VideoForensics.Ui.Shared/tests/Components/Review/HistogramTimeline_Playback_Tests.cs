using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The playback timeline: transport controls, speed cycling and flag markers over one media player.</summary>
public class HistogramTimeline_Playback_Tests : BunitContext
{
    private static readonly DateTime WindowStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc);

    public HistogramTimeline_Playback_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    private static MediaFlag Flag(MediaFlagType type, DateTime windowStartUtc)
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = type,
            DeviceId = Guid.NewGuid(),
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowStartUtc.AddSeconds(30),
            RaisedAtUtc = windowStartUtc.AddSeconds(31),
        };
    }

    private IRenderedComponent<HistogramTimeline> RenderTimeline(IReadOnlyCollection<MediaFlag> markers, EventCallback<MediaFlag> onMarkerClick = default)
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        return Render<HistogramTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.Markers, markers)
            .Add(c => c.OnMarkerClick, onMarkerClick));
    }

    [Fact]
    public void PlaybackTimeline_Renders_OneMarkerPerFlagInWindow()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderTimeline(
        [
            Flag(MediaFlagType.RssiDeviation, WindowStart.AddSeconds(60)),
            Flag(MediaFlagType.BitrateCollapse, WindowStart.AddSeconds(120)),
            Flag(MediaFlagType.RssiDeviation, WindowEnd.AddMinutes(10)),
        ]);

        Assert.Equal(2, cut.FindAll("[data-testid=timeline-marker]").Count);
    }

    [Fact]
    public void PlaybackTimeline_RateButton_CyclesThroughRates()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderTimeline([]);
        IElement rate = cut.Find("[data-testid=timeline-rate]");

        Assert.Contains("1x", rate.TextContent);
        rate.Click();
        Assert.Contains("2x", cut.Find("[data-testid=timeline-rate]").TextContent);
        cut.Find("[data-testid=timeline-rate]").Click();
        Assert.Contains("4x", cut.Find("[data-testid=timeline-rate]").TextContent);
        cut.Find("[data-testid=timeline-rate]").Click();
        Assert.Contains("8x", cut.Find("[data-testid=timeline-rate]").TextContent);
        cut.Find("[data-testid=timeline-rate]").Click();
        Assert.Contains("1x", cut.Find("[data-testid=timeline-rate]").TextContent);
        Assert.DoesNotContain("8x", cut.Find("[data-testid=timeline-rate]").TextContent);
    }

    [Fact]
    public void PlaybackTimeline_MarkerClick_RaisesOnMarkerClick()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation, WindowStart.AddSeconds(60));
        MediaFlag? clicked = null;
        IRenderedComponent<HistogramTimeline> cut = RenderTimeline(
            [flag],
            EventCallback.Factory.Create<MediaFlag>(this, f => clicked = f));

        cut.Find("[data-testid=timeline-marker]").Click();

        Assert.Same(flag, clicked);
    }
}
