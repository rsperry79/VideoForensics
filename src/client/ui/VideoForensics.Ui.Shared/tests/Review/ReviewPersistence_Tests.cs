using Bunit;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The review view survives a reload: its URL state and selected event are kept in localStorage, a bare /media
/// restores them, and entering a case writes it to the address bar.
/// </summary>
public class ReviewPersistence_Tests : ReviewTestContext
{
    private const string StorageKey = "vf.review.state";

    [Fact]
    public void ReviewPage_TabChange_SavesTheStateToLocalStorage()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        ReviewState.SetTab(ReviewTab.Events);

        // Assert
        Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "localStorage.setItem"
            && i.Arguments.Count > 1
            && i.Arguments[0]!.ToString() == StorageKey
            && i.Arguments[1]!.ToString()!.Contains("tab=Events"));
    }

    [Fact]
    public void ReviewPage_BareMediaWithSavedState_RestoresTheTab()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        JSInterop.Setup<string?>("localStorage.getItem", StorageKey)
            .SetResult("{\"query\":\"tab=Events\",\"eventId\":null}");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab));
    }

    [Fact]
    public void ReviewPage_BareMediaWithSavedEvent_RestoresTheSelectedEvent()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        Guid eventId = Guid.NewGuid();
        JSInterop.Setup<string?>("localStorage.getItem", StorageKey)
            .SetResult($"{{\"query\":\"\",\"eventId\":\"{eventId:D}\"}}");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(eventId, ReviewState.SelectedEventId));
    }

    [Fact]
    public void ReviewPage_MediaWithAQuery_IgnoresTheSavedState()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview("tab=detail");
        JSInterop.Setup<string?>("localStorage.getItem", StorageKey)
            .SetResult("{\"query\":\"tab=Events\",\"eventId\":null}");

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Equal(ReviewTab.Detail, ReviewState.ActiveTab);
    }

    [Fact]
    public void ReviewPage_CaseEntered_WritesTheCaseToTheAddressBar()
    {
        // Arrange
        AddDevices(1);
        Guid caseId = ArrangePinnedCase("Driveway", Array.Empty<VideoForensics.Data.Common.Entities.MediaItem>());
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        _ = CaseMode.EnterAsync(caseId, CancellationToken.None).GetAwaiter().GetResult();

        // Assert
        Assert.Contains("reviewcase=" + caseId.ToString("D"), CurrentUri);
    }
}
