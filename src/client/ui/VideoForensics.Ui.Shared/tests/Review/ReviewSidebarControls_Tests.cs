using Bunit;

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
    public void ReviewSidebarControls_Render_ShowsTheThreeTabsInATablist()
    {
        // Arrange and Act
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-tabs'][role='tablist']"));
        Assert.Equal(3, cut.FindAll("[data-testid='review-tabs'] [role='tab']").Count);
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
    public void ReviewSidebarControls_Render_ShowsTimeRangeCameraSelectorAndFiltersButton()
    {
        // Arrange and Act
        IRenderedComponent<ReviewSidebarControls> cut = RenderControls();

        // Assert
        _ = cut.FindComponent<ReviewTimeRange>();
        _ = Assert.Single(cut.FindAll("[data-testid='review-camera-selector']"));
        _ = Assert.Single(cut.FindAll("[data-testid='review-filters-button']"));
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
