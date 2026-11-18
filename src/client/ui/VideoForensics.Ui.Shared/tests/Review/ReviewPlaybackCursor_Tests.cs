using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>
/// The playback cursor: the timeline follows the video while it plays. It is a separate value from the seek cursor, so
/// playback never re-seeks the player and never rewrites the address bar.
/// </summary>
public class ReviewPlaybackCursor_Tests
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReviewPageState_SetPlaybackCursor_DoesNotRaiseSeekChange()
    {
        // Arrange
        var state = new ReviewPageState(TimeProvider.System);
        int seekChanges = 0;
        state.OnChange += () => seekChanges++;

        // Act
        state.SetPlaybackCursor(Start.AddMinutes(5));

        // Assert: OnChange drives the URL write and the reloads, so playback must leave it alone
        Assert.Equal(0, seekChanges);
    }

    [Fact]
    public void ReviewPageState_SetPlaybackCursor_RaisesPlaybackChange()
    {
        // Arrange
        var state = new ReviewPageState(TimeProvider.System);
        int playbackChanges = 0;
        state.OnPlaybackChange += () => playbackChanges++;

        // Act
        state.SetPlaybackCursor(Start.AddMinutes(5));

        // Assert
        Assert.Equal(1, playbackChanges);
        Assert.Equal(Start.AddMinutes(5), state.PlaybackCursor);
    }

    [Fact]
    public void ReviewPageState_SetPlaybackCursor_LeavesTheSeekCursorAlone()
    {
        // Arrange
        var state = new ReviewPageState(TimeProvider.System);
        state.SetRange(Start.AddHours(-1), Start.AddHours(1));
        state.SetCursor(Start);

        // Act
        state.SetPlaybackCursor(Start.AddMinutes(5));

        // Assert: the player's seek cursor stays where the user put it
        Assert.Equal(Start, state.Cursor);
    }

    [Fact]
    public void ReviewPageState_SetPlaybackCursor_SameTimeTwice_RaisesOnce()
    {
        // Arrange
        var state = new ReviewPageState(TimeProvider.System);
        int playbackChanges = 0;
        state.OnPlaybackChange += () => playbackChanges++;

        // Act
        state.SetPlaybackCursor(Start);
        state.SetPlaybackCursor(Start);

        // Assert
        Assert.Equal(1, playbackChanges);
    }
}
