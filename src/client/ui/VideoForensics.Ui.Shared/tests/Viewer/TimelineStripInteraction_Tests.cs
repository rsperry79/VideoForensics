using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using System.Text.RegularExpressions;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The vertical strip draws in percent of its own length, shows the time under the pointer, and seeks when a drag ends.</summary>
public class TimelineStripInteraction_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const int Length = 600;

    public TimelineStripInteraction_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<TimelineStrip> Render(IReadOnlyList<ClipWindow> clips, DateTime? cursor = null, EventCallback<DateTime> onSeek = default)
    {
        return Render<TimelineStrip>(p => p
            .Add(x => x.Orientation, TimelineOrientation.Vertical)
            .Add(x => x.LengthPx, Length)
            .Add(x => x.Clips, clips)
            .Add(x => x.WindowStartUtc, Start)
            .Add(x => x.WindowEndUtc, Start.AddMinutes(10))
            .Add(x => x.Cursor, cursor)
            .Add(x => x.OnSeek, onSeek));
    }

    private static double StylePercent(string style, string name)
    {
        Match match = Regex.Match(style, @"(?:^|;)\s*" + name + @"\s*:\s*(-?[\d.]+)%");
        Assert.True(match.Success, $"Style '{style}' has no '{name}' percent value.");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void TimelineStrip_Segments_UsePercentPositions()
    {
        IRenderedComponent<TimelineStrip> cut = Render([new ClipWindow("oldest", Start, 60)]);

        string style = cut.Find("[data-testid='timeline-segment']").GetAttribute("style")!;

        Assert.Equal(90, StylePercent(style, "top"), 6);
        Assert.Equal(10, StylePercent(style, "height"), 6);
    }

    [Fact]
    public void TimelineStrip_CursorAndTicks_UsePercentPositions()
    {
        IRenderedComponent<TimelineStrip> cut = Render([], Start.AddMinutes(3));

        string style = cut.Find("[data-testid='timeline-cursor']").GetAttribute("style")!;

        Assert.Equal(70, StylePercent(style, "top"), 6);
    }

    [Fact]
    public void TimelineStrip_PointerMove_ShowsTheTimeUnderThePointer()
    {
        IRenderedComponent<TimelineStrip> cut = Render([]);

        cut.Find("[data-testid='timeline-strip']").PointerMove(new PointerEventArgs { OffsetY = 300 });

        string text = cut.Find("[data-testid='timeline-hover']").TextContent;
        Assert.Matches(@"\d{2}:\d{2}:\d{2}", text);
    }

    [Fact]
    public void TimelineStrip_PointerLeave_HidesTheHoverTime()
    {
        IRenderedComponent<TimelineStrip> cut = Render([]);
        cut.Find("[data-testid='timeline-strip']").PointerMove(new PointerEventArgs { OffsetY = 300 });

        cut.Find("[data-testid='timeline-strip']").PointerLeave(new PointerEventArgs());

        Assert.Empty(cut.FindAll("[data-testid='timeline-hover']"));
    }

    [Fact]
    public void TimelineStrip_Drag_SeeksOnceOnRelease()
    {
        var seeks = new List<DateTime>();
        IRenderedComponent<TimelineStrip> cut = Render([], onSeek: EventCallback.Factory.Create<DateTime>(this, seeks.Add));
        var strip = cut.Find("[data-testid='timeline-strip']");

        strip.PointerDown(new PointerEventArgs { OffsetY = 600 });
        strip.PointerMove(new PointerEventArgs { OffsetY = 60 });
        Assert.Empty(seeks);
        strip.PointerUp(new PointerEventArgs { OffsetY = 60 });

        Assert.Equal([Start.AddMinutes(9)], seeks);
    }
}
