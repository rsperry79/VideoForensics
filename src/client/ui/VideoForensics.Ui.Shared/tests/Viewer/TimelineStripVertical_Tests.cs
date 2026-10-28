using System.Globalization;
using System.Text.RegularExpressions;

using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>
/// The vertical timeline strip: the newest time sits at the top, clicks map back to the time under them, the cursor is a
/// horizontal line, and hour labels are shown in the user's zone. The horizontal strip is covered by ViewerComponentTests.
/// </summary>
public class TimelineStripVertical_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const int Length = 600;

    private static readonly TimeZoneInfo Plus9 = TimeZoneInfo.CreateCustomTimeZone("test-plus9", TimeSpan.FromHours(9), "test-plus9", "test-plus9");

    public TimelineStripVertical_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Reads one pixel value (for example "top" or "height") from an inline style string.</summary>
    private static double StylePx(string style, string name)
    {
        Match match = Regex.Match(style, @"(?:^|;)\s*" + name + @"\s*:\s*(-?[\d.]+)px");
        Assert.True(match.Success, $"Style '{style}' has no '{name}' value.");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private IRenderedComponent<TimelineStrip> RenderVertical(
        IReadOnlyList<ClipWindow> clips,
        DateTime? cursor = null,
        EventCallback<DateTime> onSeek = default)
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

    /// <summary>The newest clip (ending at the window end) is drawn at the top of the strip.</summary>
    [Fact]
    public void TimelineStripVertical_NewestClip_SitsAtTheTopOfTheStrip()
    {
        var clips = new List<ClipWindow> { new("newest", Start.AddMinutes(9), 60) };

        IRenderedComponent<TimelineStrip> cut = RenderVertical(clips);

        string style = cut.Find("[data-testid='timeline-segment']").GetAttribute("style")!;
        Assert.Equal(0, StylePx(style, "top"), 6);
        Assert.Equal(60, StylePx(style, "height"), 6);
    }

    /// <summary>The oldest clip (starting at the window start) is drawn at the bottom of the strip.</summary>
    [Fact]
    public void TimelineStripVertical_OldestClip_SitsAtTheBottomOfTheStrip()
    {
        var clips = new List<ClipWindow> { new("oldest", Start, 60) };

        IRenderedComponent<TimelineStrip> cut = RenderVertical(clips);

        string style = cut.Find("[data-testid='timeline-segment']").GetAttribute("style")!;
        Assert.Equal(540, StylePx(style, "top"), 6);
        Assert.Equal(60, StylePx(style, "height"), 6);
    }

    /// <summary>Every clip gets its own segment.</summary>
    [Fact]
    public void TimelineStripVertical_Clips_RendersOneSegmentPerClip()
    {
        var clips = new List<ClipWindow> { new("a", Start, 60), new("b", Start.AddMinutes(5), 60) };

        IRenderedComponent<TimelineStrip> cut = RenderVertical(clips);

        Assert.Equal(2, cut.FindAll("[data-testid='timeline-segment']").Count);
    }

    /// <summary>With no clips the strip draws no segments, only its container.</summary>
    [Fact]
    public void TimelineStripVertical_NoClips_RendersNoSegments()
    {
        IRenderedComponent<TimelineStrip> cut = RenderVertical([]);

        Assert.Single(cut.FindAll("[data-testid='timeline-strip']"));
        Assert.Empty(cut.FindAll("[data-testid='timeline-segment']"));
        Assert.Empty(cut.FindAll("[data-testid='timeline-cursor']"));
    }

    /// <summary>A click at a height maps back to the time drawn at that height (newest at the top).</summary>
    [Fact]
    public void TimelineStripVertical_ClickNearTheTop_SeeksToTheNewerTime()
    {
        DateTime? seeked = null;
        IRenderedComponent<TimelineStrip> cut = RenderVertical(
            [],
            onSeek: EventCallback.Factory.Create<DateTime>(this, t => seeked = t));

        cut.Find("[data-testid='timeline-strip']").Click(new MouseEventArgs { OffsetY = 60 });

        // 60px from the top of a 600px strip is 90% of the way from the window start.
        Assert.Equal(Start.AddMinutes(9), seeked);
    }

    /// <summary>The cursor is drawn at its time's height, and clicking that height seeks back to the same time.</summary>
    [Fact]
    public void TimelineStripVertical_CursorClickRoundTrip_SeeksBackToTheCursorTime()
    {
        DateTime cursorTime = Start.AddMinutes(3);
        DateTime? seeked = null;
        IRenderedComponent<TimelineStrip> cut = RenderVertical(
            [],
            cursorTime,
            EventCallback.Factory.Create<DateTime>(this, t => seeked = t));

        double top = StylePx(cut.Find("[data-testid='timeline-cursor']").GetAttribute("style")!, "top");
        cut.Find("[data-testid='timeline-strip']").Click(new MouseEventArgs { OffsetY = top });

        Assert.Equal(cursorTime, seeked);
    }

    /// <summary>The cursor is a horizontal line at the cursor time's height: 3 minutes into a 10 minute window is 420px from the top.</summary>
    [Fact]
    public void TimelineStripVertical_Cursor_IsAHorizontalLineAtItsTimesHeight()
    {
        IRenderedComponent<TimelineStrip> cut = RenderVertical([], Start.AddMinutes(3));

        string style = cut.Find("[data-testid='timeline-cursor']").GetAttribute("style")!;
        Assert.Equal(420, StylePx(style, "top"), 6);
        Assert.DoesNotContain("width", style, StringComparison.Ordinal);
    }

    /// <summary>Without a cursor time no cursor line is drawn.</summary>
    [Fact]
    public void TimelineStripVertical_NoCursor_DrawsNoCursor()
    {
        IRenderedComponent<TimelineStrip> cut = RenderVertical([]);

        Assert.Empty(cut.FindAll("[data-testid='timeline-cursor']"));
    }

    /// <summary>Hour labels are formatted in the user's zone and run newest first, from the top down.</summary>
    [Fact]
    public void TimelineStripVertical_HourTicks_AreLabelledInTheUserZoneNewestFirst()
    {
        IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
            .AddCascadingValue(new UserTimeZone(Plus9))
            .Add(x => x.Orientation, TimelineOrientation.Vertical)
            .Add(x => x.LengthPx, Length)
            .Add(x => x.Clips, new List<ClipWindow>())
            .Add(x => x.WindowStartUtc, Start)
            .Add(x => x.WindowEndUtc, Start.AddHours(3)));

        string[] labels = cut.FindAll("[data-testid='timeline-tick']").Select(t => t.TextContent.Trim()).ToArray();

        // 12:00 to 15:00 UTC is 21:00 to 00:00 at UTC+9.
        Assert.Equal(["00:00", "23:00", "22:00", "21:00"], labels);
    }

    /// <summary>Each hour tick is placed at its own time's height.</summary>
    [Fact]
    public void TimelineStripVertical_HourTick_IsPlacedAtItsTimesHeight()
    {
        IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
            .Add(x => x.Orientation, TimelineOrientation.Vertical)
            .Add(x => x.LengthPx, Length)
            .Add(x => x.Clips, new List<ClipWindow>())
            .Add(x => x.WindowStartUtc, Start)
            .Add(x => x.WindowEndUtc, Start.AddHours(2)));

        // Window start 12:00 is the bottom (600px); 13:00 is halfway (300px); 14:00 is the top (0px).
        IReadOnlyList<IElement> ticks = cut.FindAll("[data-testid='timeline-tick']");
        Assert.Equal(3, ticks.Count);
        Assert.Equal(0, StylePx(ticks[0].GetAttribute("style")!, "top"), 6);
        Assert.Equal(300, StylePx(ticks[1].GetAttribute("style")!, "top"), 6);
        Assert.Equal(600, StylePx(ticks[2].GetAttribute("style")!, "top"), 6);
    }
}
