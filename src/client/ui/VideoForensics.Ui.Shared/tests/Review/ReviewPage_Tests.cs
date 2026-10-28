using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review page shell: sign-in gate, top bar labels, URL seeding and write-back, tab switching, and disposal.</summary>
public class ReviewPage_Tests : ReviewTestContext
{
    [Fact]
    public async Task ReviewPage_NotSignedIn_ShowsSignInPromptAndNoTopBar()
    {
        // Arrange
        AddDevices(2);
        await SignOutOperatorAsync();
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Contains(L["NotSignedIn"].Value, cut.Find("[data-testid='review-sign-in']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='review-top-bar']"));
    }

    [Fact]
    public void ReviewPage_SignedIn_ShowsEveryLocalizedLabel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        string topBar = cut.Find("[data-testid='review-top-bar']").TextContent;
        foreach (string key in new[]
        {
            "Back", "Live", "AllCameras", "RangeLast24Hours", "Filter", "Actions", "TabTimeline", "TabEvents", "TabDetail",
        })
        {
            Assert.Contains(L[key].Value, topBar);
        }
    }

    [Fact]
    public void ReviewPage_InitialQuery_SeedsStateAndShowsEventsPlaceholder()
    {
        // Arrange
        AddDevices(2);
        Guid device = Devices[0].Id;
        NavigateToReview($"devices={device:D}&tab=events&t=2026-10-28T11:00:00Z");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
        Assert.Equal([device], ReviewState.CameraIds);
        Assert.Equal(new DateTime(2026, 10, 28, 11, 0, 0, DateTimeKind.Utc), ReviewState.Cursor);
        Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-detail']"));
    }

    [Fact]
    public void ReviewPage_InitialQuery_KeepsEveryParsedValueInTheUrl()
    {
        // Arrange - a partial parse must not write the URL back before every value is read
        AddDevices(2);
        Guid device = Devices[0].Id;
        NavigateToReview($"devices={device:D}&tab=events&t=2026-10-28T11:00:00Z");

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Contains("tab=Events", CurrentUri);
        Assert.Contains("t=2026-10-28T11:00:00", CurrentUri);
        Assert.Contains("devices=" + device.ToString("D"), CurrentUri);
    }

    [Fact]
    public void ReviewPage_TabClick_SwitchesStateTabPlaceholderAndUrl()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        cut.Find("[data-testid='review-tab-button-events']").Click();

        // Assert
        Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
        Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Contains("tab=Events", CurrentUri);
    }

    [Fact]
    public void ReviewPage_TimelineTab_RendersTheTimelineComponentInItsPanel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Single(cut.FindComponents<ReviewTimelineTab>());
        Assert.Single(cut.FindAll("[data-testid='review-tab-timeline'] [data-testid='review-timeline-tab']"));
        Assert.DoesNotContain(L["TabPlaceholder", L["TabTimeline"].Value].Value, cut.Find("[data-testid='review-tab-timeline']").TextContent);
    }

    [Fact]
    public async Task ReviewPage_Dispose_UnsubscribesFromStateChanges()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ((IDisposable)cut.Instance).Dispose();

        // Act
        Exception? error = await Record.ExceptionAsync(() => cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Detail)));

        // Assert - the disposed page neither throws nor re-renders to the new tab
        Assert.Null(error);
        Assert.Single(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-detail']"));
    }
}
