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
    public void Parse_LocationParameter_IsRead_AndGarbageIsIgnored()
    {
        // Arrange
        Guid location = Guid.NewGuid();

        // Act
        ReviewUrlState good = ReviewUrl.Parse($"location={location:D}");
        ReviewUrlState bad = ReviewUrl.Parse("location=nope");

        // Assert
        Assert.Equal(location, good.Location);
        Assert.Null(bad.Location);
    }

    [Fact]
    public void Build_Location_RoundTripsAndIsOmittedWhenNull()
    {
        // Arrange
        Guid location = Guid.NewGuid();
        ReviewUrlState withLocation = DefaultState() with { Location = location };

        // Act
        string built = ReviewUrl.Build(withLocation);

        // Assert
        Assert.Equal($"location={location:D}", built);
        Assert.Equal(location, ReviewUrl.Parse(built).Location);
        Assert.DoesNotContain("location", ReviewUrl.Build(DefaultState()));
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

    [Fact]
    public void Parse_CaseParam_ReadsTheCaseGuid()
    {
        // Arrange
        Guid caseId = Guid.NewGuid();

        // Act
        ReviewUrlState state = ReviewUrl.Parse("reviewcase=" + caseId.ToString("D"));

        // Assert
        Assert.Equal(caseId, state.CaseId);
    }

    [Fact]
    public void Parse_GarbageCaseParam_IsIgnored()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("reviewcase=not-a-guid");

        // Assert
        Assert.Null(state.CaseId);
    }

    [Fact]
    public void ReviewUrl_ActiveCaseContextParameter_IsNotTheCaseView()
    {
        // "case" is the app-wide active-case context (CaseUrl); only "reviewcase" opens the case view.
        Guid caseId = Guid.NewGuid();

        ReviewUrlState state = ReviewUrl.Parse("case=" + caseId.ToString("D"));

        Assert.Null(state.CaseId);
    }

    [Fact]
    public void ReviewUrl_WithoutCaseView_RemovesOnlyTheReviewCaseParameter()
    {
        Guid caseId = Guid.NewGuid();
        string uri = "http://localhost/media?case=" + caseId.ToString("D") + "&reviewcase=" + caseId.ToString("D") + "&tab=Events";

        string result = ReviewUrl.WithoutCaseView(uri);

        Assert.DoesNotContain("reviewcase=", result);
        Assert.Contains("case=" + caseId.ToString("D"), result);
        Assert.Contains("tab=Events", result);
    }

    [Fact]
    public void Build_CaseId_WritesTheCaseParam()
    {
        // Arrange
        Guid caseId = Guid.NewGuid();

        // Act
        string query = ReviewUrl.Build(DefaultState() with { CaseId = caseId });

        // Assert
        Assert.Equal("reviewcase=" + caseId.ToString("D"), query);
    }

    [Fact]
    public void Build_NullCaseId_OmitsTheCaseKey()
    {
        // Act
        string query = ReviewUrl.Build(DefaultState());

        // Assert
        Assert.DoesNotContain("reviewcase=", query);
    }

    [Fact]
    public void RoundTrip_CaseId_ParseReturnsEqualState()
    {
        // Arrange
        ReviewUrlState state = DefaultState() with { CaseId = Guid.NewGuid(), FromUtc = From, ToUtc = To };

        // Act
        ReviewUrlState parsed = ReviewUrl.Parse(ReviewUrl.Build(state));

        // Assert
        Assert.Equal(state, parsed);
    }

    [Fact]
    public void Parse_EventTypesParameter_IsReadInDisplayOrderIgnoringUnknownTypes()
    {
        // Act
        ReviewUrlState state = ReviewUrl.Parse("?eventtypes=person,bogus,motion,jamming");

        // Assert
        Assert.Equal(["motion", "person", "jamming"], state.EventTypes);
    }

    [Fact]
    public void Build_EventTypes_RoundTripsThroughParse()
    {
        // Arrange
        ReviewUrlState state = DefaultState() with { EventTypes = ["on_demand", "signal_anomaly"] };

        // Act
        string query = ReviewUrl.Build(state);

        // Assert
        Assert.Equal("eventtypes=on_demand%2csignal_anomaly", query);
        Assert.Equal(state, ReviewUrl.Parse(query));
    }

    [Fact]
    public void Build_NoEventTypes_OmitsTheParameter()
    {
        Assert.DoesNotContain("eventtypes", ReviewUrl.Build(DefaultState()));
    }
}
