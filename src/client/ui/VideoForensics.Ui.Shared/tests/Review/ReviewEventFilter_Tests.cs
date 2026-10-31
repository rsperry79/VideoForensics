using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Event Filter section of the review right panel: a multi-select of event types and system types.</summary>
public class ReviewEventFilter_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewEventFilter> RenderFilter()
    {
        return Render<ReviewEventFilter>(p => p.Add(x => x.State, ReviewState));
    }

    private static string Toggle(string type)
    {
        return $"[data-testid='review-event-toggle-{type}']";
    }

    [Fact]
    public void ReviewEventFilter_Rendered_ShowsTheEventFilterHeading()
    {
        // Act
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();

        // Assert
        Assert.Equal("Event Filter", cut.Find(".review-sidebar-section-header span").TextContent);
    }

    [Fact]
    public void ReviewEventFilter_NothingSelected_ButtonShowsAllEvents()
    {
        // Act
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();

        // Assert
        Assert.Equal(L["EventTypeAll"].Value, cut.Find("[data-testid='review-event-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewEventFilter_Opened_ListsProviderTypesThenSystemTypes()
    {
        // Arrange
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();

        // Act
        cut.Find("[data-testid='review-event-button']").Click();

        // Assert
        List<string?> toggles = cut.FindAll("[data-testid^='review-event-toggle-']").Select(e => e.GetAttribute("data-testid")).ToList();
        Assert.Equal(ReviewEventFilters.SelectableEventTypes.Select(t => "review-event-toggle-" + t).Cast<string?>().ToList(), toggles);
        Assert.Contains("Signal anomalies", cut.Markup);
    }

    [Fact]
    public void ReviewEventFilter_TypesToggledOn_WritesTheStateAfterTheDebounce()
    {
        // Arrange
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();
        cut.Find("[data-testid='review-event-button']").Click();

        // Act
        cut.Find(Toggle("motion")).Change(true);
        cut.Find(Toggle("signal_anomaly")).Change(true);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(["motion", "signal_anomaly"], ReviewState.EventTypes));
    }

    [Fact]
    public void ReviewEventFilter_AllToggledOn_ClearsTheSelection()
    {
        // Arrange
        ReviewState.SetEventTypes(["motion", "jamming"]);
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();
        cut.Find("[data-testid='review-event-button']").Click();

        // Act
        cut.Find("[data-testid='review-event-all']").Change(true);

        // Assert
        cut.WaitForAssertion(() => Assert.Empty(ReviewState.EventTypes));
    }

    [Fact]
    public void ReviewEventFilter_OneSelected_ButtonNamesIt_TwoSelected_ButtonCountsThem()
    {
        // Arrange
        ReviewState.SetEventTypes(["jamming"]);
        IRenderedComponent<ReviewEventFilter> cut = RenderFilter();
        string one = cut.Find("[data-testid='review-event-button']").TextContent.Trim();

        // Act
        cut.InvokeAsync(() => ReviewState.SetEventTypes(["jamming", "motion"]));

        // Assert
        Assert.Equal(L["EventType_jamming"].Value, one);
        cut.WaitForAssertion(() => Assert.Equal(L["EventTypesSelected", 2].Value, cut.Find("[data-testid='review-event-button']").TextContent.Trim()));
    }
}
