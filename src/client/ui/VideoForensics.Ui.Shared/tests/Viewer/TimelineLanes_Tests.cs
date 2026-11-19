using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

public class TimelineLanes_Tests : BunitContext
{
    private static readonly DateTime WindowStart = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    public TimelineLanes_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void TimelineStrip_TwoCameras_DrawsEachCameraInItsOwnLane()
    {
        var clips = new List<ClipWindow>
        {
            new("a1", WindowStart.AddHours(1), 600, LaneId: "camA"),
            new("b1", WindowStart.AddHours(2), 600, LaneId: "camB"),
        };

        IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
            .Add(s => s.Orientation, TimelineOrientation.Vertical)
            .Add(s => s.Clips, clips)
            .Add(s => s.WindowStartUtc, WindowStart)
            .Add(s => s.WindowEndUtc, WindowEnd));

        var segments = cut.FindAll("[data-testid='timeline-segment']");
        Assert.Equal(2, segments.Count);
        Assert.Contains("(100% - 3.5rem) * 0)", segments[0].GetAttribute("style"));
        Assert.Contains("(100% - 3.5rem) * 0.5)", segments[1].GetAttribute("style"));
    }

    [Fact]
    public void TimelineStrip_TimelapseClip_UsesTheTimelapseColourClass()
    {
        var clips = new List<ClipWindow> { new("t1", WindowStart.AddHours(1), 600, LaneId: "camA", Kind: "Timelapse") };

        IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
            .Add(s => s.Orientation, TimelineOrientation.Vertical)
            .Add(s => s.Clips, clips)
            .Add(s => s.WindowStartUtc, WindowStart)
            .Add(s => s.WindowEndUtc, WindowEnd));

        Assert.True(cut.Find("[data-testid='timeline-segment']").ClassList.Contains("review-timeline-kind-timelapse"));
    }
}
