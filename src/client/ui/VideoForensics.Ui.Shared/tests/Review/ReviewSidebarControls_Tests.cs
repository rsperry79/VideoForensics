using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The review controls at the top of the right sidebar: the view tabs, time range, camera selector, Filters and pending tags.</summary>
public class ReviewSidebarControls_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewSidebarControls> RenderControls()
    {
        AddDevices(2);
        return Render<ReviewSidebarControls>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf))
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList()));
    }

    [Fact]
    public void ReviewSidebarControls_Render_ShowsTheTimelineAndEventsTabsInATablist()
    {
        // Arrange and Act
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();

        // Assert: Events shows every system event, so the Detail tab is gone
        Assert.Single(cut.FindAll("[data-testid='review-tabs'][role='tablist']"));
        string[] tabIds = cut.FindAll("[data-testid='review-tabs'] [role='tab']")
            .Select(tab => tab.GetAttribute("data-testid")!)
            .ToArray();
        Assert.Equal(["review-tab-button-timeline", "review-tab-button-events"], tabIds);
        Assert.Empty(cut.FindAll("[data-testid='review-tab-button-detail']"));
    }

    [Fact]
    public void ReviewSidebarControls_TabClick_SetsTheActiveTab()
    {
        // Arrange
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();

        // Act
        cut.Find("[data-testid='review-tab-button-events']").Click();

        // Assert
        Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewSidebarControls_LiveClick_NavigatesToTheLiveViewPage()
    {
        // Arrange
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // Act
        cut.Find("[data-testid='review-tab-button-live']").Click();

        // Assert: Live is a page, so the review tab stays where it was
        Assert.EndsWith("/live-view", navigation.Uri);
        Assert.Equal(ReviewTab.Timeline, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewSidebarControls_Render_ShowsTimeRangeWithoutCameraSelectorOrFiltersButton()
    {
        // Arrange and Act
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();

        // Assert: the camera selector sits under Locations in the filter section; the Filters button is gone
        _ = cut.FindComponent<ReviewTimeRange>();
        Assert.Empty(cut.FindAll("[data-testid='review-camera-selector']"));
        Assert.Empty(cut.FindAll("[data-testid='review-filters-button']"));
    }

    [Fact]
    public void ReviewSidebarControls_PendingTagsExist_ShowsTheBadge()
    {
        // Arrange
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();
        Assert.Empty(cut.FindAll("[data-testid='review-pending-tags-count']"));

        // Act
        _ = cut.InvokeAsync(() => Services.GetRequiredService<PendingEvidenceTags>().Add(new PendingTag(
            CaseItemKindDto.Media, Guid.NewGuid(), Devices[0].Id, FixedNowUtc, PendingTagFakes.Reason, "actor-1")));

        // Assert
        _ = Assert.Single(cut.FindAll("[data-testid='review-pending-tags-count']"));
    }
}
