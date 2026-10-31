using Bunit;

using Microsoft.AspNetCore.Components.Web;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The right-panel timeline: draws the published clips, seeks the cursor on click, and follows model and state changes.</summary>
public class ReviewTimelinePanel_Tests : ReviewTestContext
{
    private static readonly DateTime WindowStart = FixedNowUtc.AddMinutes(-10);

    private void PublishWindow(params ClipWindow[] clips)
    {
        TimelineModel.Publish(clips, WindowStart, FixedNowUtc);
    }

    [Fact]
    public void ReviewTimelinePanel_PublishedClips_RendersTheVerticalStripOverTheModelRange()
    {
        // Arrange
        PublishWindow(
            new ClipWindow("a", WindowStart.AddMinutes(1), 60),
            new ClipWindow("b", WindowStart.AddMinutes(5), 60));

        // Act
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Assert
        Assert.Equal(2, cut.FindAll("[data-testid='timeline-segment']").Count);
        TimelineStrip strip = cut.FindComponent<TimelineStrip>().Instance;
        Assert.Equal(TimelineOrientation.Vertical, strip.Orientation);
        Assert.Equal(WindowStart, strip.WindowStartUtc);
        Assert.Equal(FixedNowUtc, strip.WindowEndUtc);
    }

    [Fact]
    public void ReviewTimelinePanel_ClickOnTheStrip_SetsTheCursorToTheTimeUnderTheClick()
    {
        // Arrange
        PublishWindow();
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Act: 60px from the top of the strip is 90% through the 10 minute window
        cut.Find("[data-testid='timeline-strip']").Click(new MouseEventArgs { OffsetY = 60 });

        // Assert
        Assert.Equal(FixedNowUtc.AddMinutes(-1), ReviewState.Cursor);
    }

    [Fact]
    public void ReviewTimelinePanel_NoClips_ShowsTheEmptyMessage()
    {
        // Arrange
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Act & Assert
        Assert.Equal(L["TimelinePanelEmpty"].Value, cut.Find("[data-testid='review-timeline-panel-empty']").TextContent);
    }

    [Fact]
    public void ReviewTimelinePanel_WithClips_HidesTheEmptyMessage()
    {
        // Arrange
        PublishWindow(new ClipWindow("a", WindowStart.AddMinutes(1), 60));

        // Act
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-timeline-panel-empty']"));
    }

    [Fact]
    public void ReviewTimelinePanel_ModelPublishesClips_RerendersTheSegments()
    {
        // Arrange
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Act
        cut.InvokeAsync(() => PublishWindow(new ClipWindow("a", WindowStart.AddMinutes(1), 60)));

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='timeline-segment']")));
    }

    [Fact]
    public void ReviewTimelinePanel_CursorMoves_RedrawsTheCursorLineAtItsHeight()
    {
        // Arrange
        PublishWindow();
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();
        Assert.Empty(cut.FindAll("[data-testid='timeline-cursor']"));

        // Act: 5 minutes into a 10 minute window is halfway, 300px from the top of a 600px strip
        cut.InvokeAsync(() => ReviewState.SetCursor(FixedNowUtc.AddMinutes(-5)));

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='timeline-cursor']")));
        Assert.Contains("top:300px", cut.Find("[data-testid='timeline-cursor']").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewTimelinePanel_Disposed_UnsubscribesFromModelAndStateChanges()
    {
        // Arrange
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();
        ((IDisposable)cut.Instance).Dispose();

        // Act
        Exception? error = Record.Exception(() => cut.InvokeAsync(() =>
        {
            PublishWindow(new ClipWindow("a", WindowStart.AddMinutes(1), 60));
            ReviewState.SetCursor(FixedNowUtc.AddMinutes(-5));
        }).GetAwaiter().GetResult());

        // Assert: the disposed panel neither throws nor re-renders
        Assert.Null(error);
        Assert.Empty(cut.FindAll("[data-testid='timeline-segment']"));
        Assert.Empty(cut.FindAll("[data-testid='timeline-cursor']"));
    }
}
