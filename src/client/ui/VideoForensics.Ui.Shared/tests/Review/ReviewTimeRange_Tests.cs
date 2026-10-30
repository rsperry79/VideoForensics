using AngleSharp.Dom;

using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Time Range section in the media right sidebar: the range dropdown, and From/To pickers for a custom range.</summary>
public class ReviewTimeRange_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewTimeRange> RenderSection()
    {
        return Render<ReviewTimeRange>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.UtcNow, () => FixedNowUtc));
    }

    [Fact]
    public void ReviewTimeRange_Rendered_OffersHourDayWeekThirtyDaysAndCustom()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();

        // Assert
        string[] values = cut.FindAll("[data-testid='review-range'] option").Select(o => o.GetAttribute("value")!).ToArray();
        Assert.Equal(new[] { "hour", "day", "week", "month", "custom" }, values);
    }

    [Fact]
    public void ReviewTimeRange_PresetRange_ShowsNoDatePickers()
    {
        // Arrange
        ReviewState.SetRange(FixedNowUtc.AddDays(-1), FixedNowUtc);

        // Act
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();

        // Assert
        Assert.Empty(cut.FindAll("input[type='date']"));
    }

    [Fact]
    public void ReviewTimeRange_ThirtyDays_SetsRangeEndingNow()
    {
        // Arrange
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();

        // Act
        cut.Find("[data-testid='review-range']").Change("month");

        // Assert
        Assert.Equal(FixedNowUtc.AddDays(-30), ReviewState.FromUtc);
        Assert.Equal(FixedNowUtc, ReviewState.ToUtc);
    }

    [Fact]
    public void ReviewTimeRange_CustomChosen_ShowsFromAndToPickers()
    {
        // Arrange
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();

        // Act
        cut.Find("[data-testid='review-range']").Change("custom");

        // Assert
        Assert.Equal(2, cut.FindAll("input[type='date']").Count);
        Assert.True(cut.Find("option[value='custom']").HasAttribute("selected"));
    }

    [Fact]
    public void ReviewTimeRange_CustomDates_SetRangeFromStartOfFromDayToEndOfToDay()
    {
        // Arrange
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();
        cut.Find("[data-testid='review-range']").Change("custom");

        // Act
        cut.Find("[data-testid='review-range-from']").Change("2026-10-20");
        cut.Find("[data-testid='review-range-to']").Change("2026-10-25");

        // Assert - the To day is included, so the window ends at the start of the day after it
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), ReviewState.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), ReviewState.ToUtc);
    }

    [Fact]
    public void ReviewTimeRange_CustomWindowAlreadySet_ShowsPickersAndSelectsCustom()
    {
        // Arrange
        ReviewState.SetRange(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));

        // Act
        IRenderedComponent<ReviewTimeRange> cut = RenderSection();

        // Assert
        Assert.Equal(2, cut.FindAll("input[type='date']").Count);
        Assert.True(cut.Find("option[value='custom']").HasAttribute("selected"));
    }
}
