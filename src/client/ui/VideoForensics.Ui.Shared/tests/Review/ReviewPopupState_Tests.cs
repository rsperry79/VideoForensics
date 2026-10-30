using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

public class ReviewPopupState_Tests
{
    [Fact]
    public void ReviewPopupState_NothingOpened_NoPopupIsOpen()
    {
        // Arrange and Act
        var state = new ReviewPopupState();

        // Assert
        Assert.Null(state.Open);
        Assert.False(state.IsOpen(ReviewPopupState.Camera));
    }

    [Fact]
    public void ReviewPopupState_Toggle_OpensThePopup()
    {
        // Arrange
        var state = new ReviewPopupState();

        // Act
        state.Toggle(ReviewPopupState.Camera);

        // Assert
        Assert.Equal(ReviewPopupState.Camera, state.Open);
        Assert.True(state.IsOpen(ReviewPopupState.Camera));
    }

    [Fact]
    public void ReviewPopupState_ToggleAnotherPopup_ClosesTheFirst()
    {
        // Arrange
        var state = new ReviewPopupState();
        state.Toggle(ReviewPopupState.Camera);

        // Act
        state.Toggle(ReviewPopupState.Filter);

        // Assert
        Assert.Equal(ReviewPopupState.Filter, state.Open);
        Assert.False(state.IsOpen(ReviewPopupState.Camera));
        Assert.True(state.IsOpen(ReviewPopupState.Filter));
    }

    [Fact]
    public void ReviewPopupState_ToggleTheOpenPopup_ClosesIt()
    {
        // Arrange
        var state = new ReviewPopupState();
        state.Toggle(ReviewPopupState.Actions);

        // Act
        state.Toggle(ReviewPopupState.Actions);

        // Assert
        Assert.Null(state.Open);
    }

    [Fact]
    public void ReviewPopupState_CloseWithOtherId_LeavesTheOpenPopupOpen()
    {
        // Arrange
        var state = new ReviewPopupState();
        state.Toggle(ReviewPopupState.Pending);

        // Act
        state.Close(ReviewPopupState.Filter);

        // Assert
        Assert.Equal(ReviewPopupState.Pending, state.Open);
    }

    [Fact]
    public void ReviewPopupState_CloseWithItsOwnId_ClosesIt()
    {
        // Arrange
        var state = new ReviewPopupState();
        state.Toggle(ReviewPopupState.Pending);

        // Act
        state.Close(ReviewPopupState.Pending);

        // Assert
        Assert.Null(state.Open);
    }

    [Fact]
    public void ReviewPopupState_CloseWithNoId_ClosesWhicheverIsOpen()
    {
        // Arrange
        var state = new ReviewPopupState();
        state.Toggle(ReviewPopupState.Camera);

        // Act
        state.Close();

        // Assert
        Assert.Null(state.Open);
    }

    [Fact]
    public void ReviewPopupState_OnChange_RaisesOnlyWhenTheStateChanges()
    {
        // Arrange
        var state = new ReviewPopupState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act and Assert: closing when nothing is open changes nothing
        state.Close();
        state.Close(ReviewPopupState.Camera);
        Assert.Equal(0, raised);

        // Act and Assert: opening changes the state
        state.Toggle(ReviewPopupState.Camera);
        Assert.Equal(1, raised);

        // Act and Assert: closing another popup changes nothing
        state.Close(ReviewPopupState.Filter);
        Assert.Equal(1, raised);

        // Act and Assert: closing the open popup changes the state
        state.Close(ReviewPopupState.Camera);
        Assert.Equal(2, raised);
    }
}
