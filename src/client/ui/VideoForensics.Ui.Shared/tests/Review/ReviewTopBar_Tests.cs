using AngleSharp.Dom;

using Microsoft.Extensions.DependencyInjection;
using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review top bar: navigation buttons, range presets, disabled later-batch controls, and the tab switch.</summary>
public class ReviewTopBar_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewTopBar> RenderBar()
    {
        AddDevices(2);
        return Render<ReviewTopBar>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf))
            .Add(p => p.UtcNow, () => FixedNowUtc));
    }

    [Fact]
    public void ReviewTopBar_BackClick_NavigatesToTheLegacyMediaList()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-back']").Click();

        // Assert
        Assert.Equal("http://localhost/media", CurrentUri);
    }

    [Fact]
    public void ReviewTopBar_LiveClick_NavigatesToTheLiveView()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-live']").Click();

        // Assert
        Assert.Equal("http://localhost/live-view", CurrentUri);
    }

    [Fact]
    public void ReviewTopBar_Actions_IsEnabledAndOpensTheMenu()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        IElement actions = cut.Find("[data-testid='review-actions']");
        Assert.False(actions.HasAttribute("disabled"));

        // Act: the button opens the actions popup
        actions.Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-actions-popup']"));
    }

    [Fact]
    public void ReviewTopBar_FilterButton_IsEnabledAndTogglesThePanel()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        Assert.False(cut.Find("[data-testid='review-filter']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='review-filter-panel']"));

        // Act
        cut.Find("[data-testid='review-filter']").Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-filter-panel']"));

        // Act again
        cut.Find("[data-testid='review-filter']").Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-filter-panel']"));
    }

    [Fact]
    public void ReviewTopBar_NoFilterSet_ShowsNoActiveIndicator()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-filter-active']"));
    }

    [Fact]
    public void ReviewTopBar_ProviderFilterSet_ShowsTheActiveIndicator()
    {
        // Arrange
        ReviewState.SetFilters("Ring", null);

        // Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        IElement indicator = cut.Find("[data-testid='review-filter-active']");
        Assert.Equal(L["FilterActive"].Value, indicator.GetAttribute("aria-label"));
    }

    [Fact]
    public void ReviewTopBar_AccountFilterSet_ShowsTheActiveIndicator()
    {
        // Arrange
        ReviewState.SetFilters(null, Guid.NewGuid());

        // Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-filter-active']"));
    }

    [Fact]
    public void ReviewTopBar_RangePresetLastSevenDays_SetsStateRangeEndingNow()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-range']").Change("week");

        // Assert
        Assert.Equal(FixedNowUtc.AddDays(-7), ReviewState.FromUtc);
        Assert.Equal(FixedNowUtc, ReviewState.ToUtc);
    }

    [Fact]
    public void ReviewTopBar_TabClick_SetsActiveTabAndHighlightsIt()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-tab-button-detail']").Click();

        // Assert
        Assert.Equal(ReviewTab.Detail, ReviewState.ActiveTab);
        Assert.Contains("active", cut.Find("[data-testid='review-tab-button-detail']").ClassName);
        Assert.DoesNotContain("active", cut.Find("[data-testid='review-tab-button-timeline']").ClassName);
    }

    [Fact]
    public void ReviewTopBar_PendingTagsExist_ShowsTheBadge()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        Assert.Empty(cut.FindAll("[data-testid='review-pending-tags-count']"));

        // Act
        cut.InvokeAsync(() => Services.GetRequiredService<PendingEvidenceTags>().Add(new PendingTag(
            CaseItemKind.Media, Guid.NewGuid(), Devices[0].Id, FixedNowUtc, PendingTagFakes.Reason, "actor-1")));

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-pending-tags-count']"));
    }
}
