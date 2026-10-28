using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

public class ReviewUrl_Tests
{
    private static readonly DateTime From = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cursor = new(2026, 10, 28, 9, 30, 15, DateTimeKind.Utc);

    private static ReviewUrlState DefaultState()
    {
        return new ReviewUrlState([], null, null, null, ReviewTab.Timeline, null, null);
    }

    [Fact]
    public void Parse_NullQuery_ReturnsDefaults()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse(null);

        // Assert
        Assert.Equal(DefaultState(), state);
    }

    [Fact]
    public void Parse_GarbageGuidDateAndTab_AreIgnoredAndNeverThrow()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        string query = $"devices=not-a-guid,{device:D}&from=garbage&to=2026-13-45&t=yesterday&tab=sideways&account=nope";

        // Act
        ReviewUrlState state = ReviewUrl.Parse(query);

        // Assert
        Assert.Equal([device], state.Devices);
        Assert.Null(state.FromUtc);
        Assert.Null(state.ToUtc);
        Assert.Null(state.CursorUtc);
        Assert.Equal(ReviewTab.Timeline, state.Tab);
        Assert.Null(state.Account);
    }

    [Fact]
    public void Parse_UnknownParameters_AreIgnored()
    {
        // Arrange
        Guid device = Guid.NewGuid();

        // Act
        ReviewUrlState state = ReviewUrl.Parse($"foo=bar&q=search&devices={device:D}");

        // Assert
        Assert.Equal([device], state.Devices);
    }

    [Fact]
    public void Parse_TwelveDevices_TruncatesToNine()
    {
        // Arrange
        List<Guid> ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();
        string query = "devices=" + string.Join(",", ids.Select(id => id.ToString("D")));

        // Act
        ReviewUrlState state = ReviewUrl.Parse(query);

        // Assert
        Assert.Equal(ids.Take(9).ToList(), state.Devices);
    }

    [Fact]
    public void Parse_LeadingQuestionMark_IsAccepted()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("?tab=events");

        // Assert
        Assert.Equal(ReviewTab.Events, state.Tab);
    }

    [Fact]
    public void Parse_TabNameInAnyCase_ReturnsTab()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("tab=DETAIL");

        // Assert
        Assert.Equal(ReviewTab.Detail, state.Tab);
    }

    [Fact]
    public void Parse_NumericTab_IsIgnoredAsTimeline()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("tab=1");

        // Assert
        Assert.Equal(ReviewTab.Timeline, state.Tab);
    }

    [Fact]
    public void Parse_ScopeDateOnly_ReadsAsUtcMidnight()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("from=2026-10-27");

        // Assert
        Assert.Equal(new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc), state.FromUtc);
    }

    [Fact]
    public void Build_DefaultState_ReturnsEmptyString()
    {
        // Act
        string query = ReviewUrl.Build(DefaultState());

        // Assert
        Assert.Equal(string.Empty, query);
    }

    [Fact]
    public void Build_TimelineTab_OmitsTabKey()
    {
        // Act
        string query = ReviewUrl.Build(DefaultState());

        // Assert
        Assert.DoesNotContain("tab=", query);
    }

    [Fact]
    public void Build_NonDefaultTab_WritesTabName()
    {
        // Arrange
        ReviewUrlState state = DefaultState() with { Tab = ReviewTab.Events };

        // Act
        string query = ReviewUrl.Build(state);

        // Assert
        Assert.Equal("tab=Events", query);
    }

    [Fact]
    public void Build_Cursor_WritesRoundTripUtcFormat()
    {
        // Arrange
        ReviewUrlState state = DefaultState() with { CursorUtc = Cursor };

        // Act
        string query = ReviewUrl.Build(state);

        // Assert
        Assert.Equal("t=2026-10-28T09:30:15.0000000Z", query);
    }

    [Fact]
    public void Build_FromAndTo_WriteScopeInstantFormat()
    {
        // Arrange
        ReviewUrlState state = DefaultState() with { FromUtc = From, ToUtc = To };

        // Act
        string query = ReviewUrl.Build(state);

        // Assert
        Assert.Contains("from=2026-10-27T12:00:00Z", query);
        Assert.Contains("to=2026-10-28T12:00:00Z", query);
    }

    [Fact]
    public void RoundTrip_FullState_ParseReturnsEqualState()
    {
        // Arrange
        ReviewUrlState state = new(
            [Guid.NewGuid(), Guid.NewGuid()],
            From,
            To,
            Cursor,
            ReviewTab.Detail,
            "Ring Co",
            Guid.NewGuid());

        // Act
        ReviewUrlState parsed = ReviewUrl.Parse(ReviewUrl.Build(state));

        // Assert
        Assert.Equal(state, parsed);
    }

    [Fact]
    public void RoundTrip_DefaultState_ParseReturnsEqualState()
    {
        // Act
        ReviewUrlState parsed = ReviewUrl.Parse(ReviewUrl.Build(DefaultState()));

        // Assert
        Assert.Equal(DefaultState(), parsed);
    }
}
