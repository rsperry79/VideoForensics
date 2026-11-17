using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using System.Globalization;
using System.Text.RegularExpressions;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The vertical timeline strip as the review panel uses it: hour ticks scale with the window, labels keep their spacing, the slider is keyboard operable.</summary>
public class TimelineStripU7_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    public TimelineStripU7_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<TimelineStrip> RenderVertical(DateTime windowStart, DateTime windowEnd, DateTime? cursor = null, EventCallback<DateTime> onSeek = default)
    {
        return Render<TimelineStrip>(p => p
            .Add(x => x.Orientation, TimelineOrientation.Vertical)
            .Add(x => x.LengthPx, 600)
            .Add(x => x.Clips, new List<ClipWindow>())
            .Add(x => x.WindowStartUtc, windowStart)
            .Add(x => x.WindowEndUtc, windowEnd)
            .Add(x => x.Cursor, cursor)
            .Add(x => x.Label, "Timeline")
            .Add(x => x.OnSeek, onSeek));
    }

    /// <summary>Distance in pixels between the first two tick labels, from their top offsets.</summary>
    private static double TickSpacingPx(IEnumerable<AngleSharp.Dom.IElement> ticks)
    {
        var tops = ticks.Select(t => TopPx(t.GetAttribute("style")!)).OrderBy(v => v).ToList();
        return tops.Count < 2 ? double.MaxValue : tops[1] - tops[0];
    }

    private static double TopPx(string style)
    {
        // Positions are percent of the strip; the strip under test is 600px long.
        Match match = Regex.Match(style, @"top:\s*(-?[\d.]+)%");
        Assert.True(match.Success, $"Style '{style}' has no top in percent.");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 6;
    }

    [Fact]
    public void TimelineStripVertical_SevenDayWindow_UsesSixHourTicks()
    {
        // Arrange: 7 days, so 6 hour ticks keep the labels readable
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(7));

        // Act
        int count = cut.FindAll("[data-testid='timeline-tick']").Count;

        // Assert: about 28 ticks (one every six hours), never one per hour
        Assert.InRange(count, 27, 29);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public void TimelineStripVertical_AnyWindow_KeepsTickLabelsAtLeast14pxApart(int days)
    {
        // Arrange
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(days));

        // Act
        double spacing = TickSpacingPx(cut.FindAll("[data-testid='timeline-tick']"));

        // Assert
        Assert.True(spacing >= 14, $"Tick spacing {spacing}px is under 14px for a {days}-day window.");
    }

    [Fact]
    public void TimelineStripVertical_ThirtyDayWindow_UsesDailyTicks()
    {
        // Arrange
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(30));

        // Act
        int count = cut.FindAll("[data-testid='timeline-tick']").Count;

        // Assert
        Assert.InRange(count, 29, 31);
    }

    [Fact]
    public void TimelineStripVertical_Vertical_HasSliderRoleAndValueText()
    {
        // Arrange
        DateTime cursor = Start.AddHours(12);
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(1), cursor);

        // Act
        IElement strip = cut.Find("[data-testid='timeline-strip']");

        // Assert
        Assert.Equal("slider", strip.GetAttribute("role"));
        Assert.Equal("0", strip.GetAttribute("tabindex"));
        Assert.False(string.IsNullOrWhiteSpace(strip.GetAttribute("aria-valuetext")));
    }

    [Fact]
    public async Task TimelineStripVertical_ArrowDown_SeeksOneStepOlder()
    {
        // Arrange
        DateTime cursor = Start.AddHours(12);
        DateTime? seeked = null;
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(1), cursor,
            EventCallback.Factory.Create<DateTime>(this, t => seeked = t));

        // Act
        await cut.Find("[data-testid='timeline-strip']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert: one minute older, the key moves toward the bottom where older times sit
        Assert.Equal(cursor.AddMinutes(-1), seeked);
    }

    [Fact]
    public async Task TimelineStripVertical_PageUp_SeeksTenStepsNewer()
    {
        // Arrange
        DateTime cursor = Start.AddHours(12);
        DateTime? seeked = null;
        IRenderedComponent<TimelineStrip> cut = RenderVertical(Start, Start.AddDays(1), cursor,
            EventCallback.Factory.Create<DateTime>(this, t => seeked = t));

        // Act
        await cut.Find("[data-testid='timeline-strip']").KeyDownAsync(new KeyboardEventArgs { Key = "PageUp" });

        // Assert
        Assert.Equal(cursor.AddMinutes(10), seeked);
    }
}
