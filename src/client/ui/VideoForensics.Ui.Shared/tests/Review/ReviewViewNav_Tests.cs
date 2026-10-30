using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review view nav in the left sidebar: the three view tabs and the Live link.</summary>
public class ReviewViewNav_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewViewNav> RenderNav()
    {
        return Render<ReviewViewNav>();
    }

    [Fact]
    public void ReviewViewNav_Rendered_ShowsTheThreeTabsAndLiveWithLocalizedLabels()
    {
        // Arrange and Act
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-view-nav']"));
        Assert.Equal(L["TabsLabel"].Value, cut.Find("[data-testid='review-view-nav'] [role='tablist']").GetAttribute("aria-label"));
        Assert.Equal(L["TabTimeline"].Value, cut.Find("[data-testid='review-tab-button-timeline']").TextContent.Trim());
        Assert.Equal(L["TabEvents"].Value, cut.Find("[data-testid='review-tab-button-events']").TextContent.Trim());
        Assert.Equal(L["TabDetail"].Value, cut.Find("[data-testid='review-tab-button-detail']").TextContent.Trim());
        Assert.Equal(L["Live"].Value, cut.Find("[data-testid='review-live']").TextContent.Trim());
    }

    [Fact]
    public void ReviewViewNav_ActiveTab_GetsActiveClassAndAriaSelected()
    {
        // Arrange
        ReviewState.SetTab(ReviewTab.Events);

        // Act
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Assert
        IElement events = cut.Find("[data-testid='review-tab-button-events']");
        Assert.Contains("e-primary", events.ClassName);
        Assert.Equal("true", events.GetAttribute("aria-selected"));
        IElement timeline = cut.Find("[data-testid='review-tab-button-timeline']");
        Assert.DoesNotContain("e-primary", timeline.ClassName);
        Assert.Equal("false", timeline.GetAttribute("aria-selected"));
    }

    [Fact]
    public void ReviewViewNav_TabClick_SetsActiveTabAndMovesTheHighlight()
    {
        // Arrange
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Act
        cut.Find("[data-testid='review-tab-button-detail']").Click();

        // Assert
        Assert.Equal(ReviewTab.Detail, ReviewState.ActiveTab);
        Assert.Contains("e-primary", cut.Find("[data-testid='review-tab-button-detail']").ClassName);
        Assert.DoesNotContain("e-primary", cut.Find("[data-testid='review-tab-button-timeline']").ClassName);
    }

    [Fact]
    public void ReviewViewNav_StateChangedExternally_MovesTheHighlight()
    {
        // Arrange
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Act
        cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert
        Assert.Contains("e-primary", cut.Find("[data-testid='review-tab-button-events']").ClassName);
    }

    [Fact]
    public void ReviewViewNav_LiveClick_NavigatesToTheLiveView()
    {
        // Arrange
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Act
        cut.Find("[data-testid='review-live']").Click();

        // Assert
        Assert.Equal("http://localhost/live-view", CurrentUri);
    }

    [Fact]
    public void ReviewViewNav_LiveClick_WithAShownCamera_OpensLiveViewOnThatCamera()
    {
        // Arrange
        var camera = Guid.NewGuid();
        ReviewState.SetCameras([camera, Guid.NewGuid()]);
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Act
        cut.Find("[data-testid='review-live']").Click();

        // Assert
        Assert.Equal($"http://localhost/live-view?device={camera}", CurrentUri);
    }

    [Fact]
    public void ReviewViewNav_OnTheLivePage_HighlightsLiveAndNoTab()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("/live-view");

        // Act
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Assert
        Assert.Contains("e-primary", cut.Find("[data-testid='review-live']").ClassName);
        foreach (string tab in new[] { "timeline", "events", "detail" })
        {
            Assert.DoesNotContain("e-primary", cut.Find($"[data-testid='review-tab-button-{tab}']").ClassName);
        }
    }

    [Theory]
    [InlineData("events", "http://localhost/media?tab=Events")]
    [InlineData("detail", "http://localhost/media?tab=Detail")]
    [InlineData("timeline", "http://localhost/media")]
    public void ReviewViewNav_TabClickOnTheLivePage_NavigatesToTheReviewPageOnThatTab(string tab, string expected)
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("/live-view");
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Act
        cut.Find($"[data-testid='review-tab-button-{tab}']").Click();

        // Assert
        Assert.Equal(expected, CurrentUri);
    }

    [Fact]
    public void ReviewViewNav_Live_IsNotATabAndNeverActive()
    {
        // Arrange and Act
        IRenderedComponent<ReviewViewNav> cut = RenderNav();

        // Assert
        IElement live = cut.Find("[data-testid='review-live']");
        Assert.Null(live.GetAttribute("role"));
        Assert.DoesNotContain("e-primary", live.ClassName);
    }

    [Fact]
    public void ReviewViewNav_Dispose_UnsubscribesFromStateChanges()
    {
        // Arrange
        IRenderedComponent<ReviewViewNav> cut = RenderNav();
        ((IDisposable)cut.Instance).Dispose();

        // Act
        Exception? error = Record.Exception(() => ReviewState.SetTab(ReviewTab.Detail));

        // Assert
        Assert.Null(error);
    }
}
