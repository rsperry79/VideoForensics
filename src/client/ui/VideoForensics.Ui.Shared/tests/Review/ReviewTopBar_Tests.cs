using AngleSharp.Dom;

using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;

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
    public void ReviewTopBar_FilterAndActions_AreDisabledWithALocalizedTooltip()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        IElement filter = cut.Find("[data-testid='review-filter']");
        IElement actions = cut.Find("[data-testid='review-actions']");
        Assert.True(filter.HasAttribute("disabled"));
        Assert.True(actions.HasAttribute("disabled"));
        Assert.Equal(L["FilterAvailableLater"].Value, filter.GetAttribute("title"));
        Assert.Equal(L["ActionsAvailableLater"].Value, actions.GetAttribute("title"));
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
}
