using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Api.Contracts;
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

    private IRenderedComponent<HistogramTimeline> RenderTimeline(IReadOnlyCollection<MediaFlag> markers, EventCallback<MediaFlagDto> onMarkerClick = default)
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        return Render<HistogramTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.Markers, markers.Select(m => m.ToDto()).ToList())
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
        MediaFlagDto? clicked = null;
        IRenderedComponent<HistogramTimeline> cut = RenderTimeline(
            [flag],
            EventCallback.Factory.Create<MediaFlagDto>(this, f => clicked = f));

        cut.Find("[data-testid=timeline-marker]").Click();

        Assert.Equal(flag.Id, clicked!.Id);
    }

    /// <summary>Renders the timeline over a playing clip whose playhead is <paramref name="playheadSeconds"/> into the window.</summary>
    private IRenderedComponent<HistogramTimeline> RenderWithPlayhead(string? eventType, double playheadSeconds)
    {
        Bunit.BunitJSModuleInterop module = JSInterop.SetupModule("./_content/VideoForensics.Client.Ui.Shared/scripts/media-player.js");
        _ = module.Setup<double>("getCurrentTime", _ => true).SetResult(playheadSeconds);
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>(p => p
            .Add(c => c.Source, "/media/clip.mp4")
            .Add(c => c.SourceStartUtc, WindowStart));
        return Render<HistogramTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.EventType, eventType));
    }

    [Fact]
    public void PlaybackTimeline_EventType_ShowsLabelAfterNow()
    {
        IRenderedComponent<HistogramTimeline> cut = RenderWithPlayhead("person", 60);

        cut.WaitForAssertion(() => Assert.Equal("· Person", cut.Find("[data-testid=timeline-event-type]").TextContent.Trim()));
        Assert.NotNull(cut.Find("[data-testid=timeline-now] + [data-testid=timeline-event-type]"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PlaybackTimeline_BlankEventType_HidesLabel(string? eventType)
    {
        IRenderedComponent<HistogramTimeline> cut = RenderWithPlayhead(eventType, 60);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=timeline-now]")));
        Assert.Empty(cut.FindAll("[data-testid=timeline-event-type]"));
    }

    [Fact]
    public void PlaybackTimeline_EventTypeWithoutPlayhead_HidesLabel()
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        IRenderedComponent<HistogramTimeline> cut = Render<HistogramTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.EventType, "person"));

        Assert.Empty(cut.FindAll("[data-testid=timeline-now]"));
        Assert.Empty(cut.FindAll("[data-testid=timeline-event-type]"));
    }
}
