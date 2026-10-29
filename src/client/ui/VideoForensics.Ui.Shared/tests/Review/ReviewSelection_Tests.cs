using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The current review selection the Actions menu acts on: the covering clip, the selected event, the primary camera, and the cursor.</summary>
public class ReviewSelection_Tests
{
    private static readonly DateTime NowUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly ReviewPageState _state;
    private readonly ReviewTimelineModel _timeline = new();
    private readonly Guid _primary = Guid.NewGuid();
    private readonly Guid _secondary = Guid.NewGuid();

    public ReviewSelection_Tests()
    {
        _state = new ReviewPageState(new FakeTimeProvider(NowUtc));
        _state.SetCameras([_primary, _secondary]);
    }

    private ReviewSelection CreateSelection()
    {
        return new ReviewSelection(_state, _timeline);
    }

    private void PublishClip(string clipId, DateTime startUtc, double durationSeconds)
    {
        _timeline.Publish(
            [new ClipWindow(clipId, startUtc, durationSeconds)],
            NowUtc.AddHours(-24),
            NowUtc);
    }

    [Fact]
    public void ReviewSelection_CursorInsideClip_ReturnsThatClipsMediaItemId()
    {
        // Arrange
        Guid clipId = Guid.NewGuid();
        DateTime clipStart = NowUtc.AddMinutes(-60);
        PublishClip(clipId.ToString("D"), clipStart, 600);
        _state.SetCursor(clipStart.AddSeconds(100));

        // Act
        Guid? mediaItemId = CreateSelection().MediaItemId;

        // Assert
        Assert.Equal(clipId, mediaItemId);
    }

    [Fact]
    public void ReviewSelection_NoCursor_ReturnsNullMediaItemIdAndAtUtc()
    {
        // Arrange
        PublishClip(Guid.NewGuid().ToString("D"), NowUtc.AddMinutes(-60), 600);

        // Act
        ReviewSelection selection = CreateSelection();

        // Assert
        Assert.Null(selection.MediaItemId);
        Assert.Null(selection.AtUtc);
    }

    [Fact]
    public void ReviewSelection_CursorInGapBetweenClips_ReturnsNullMediaItemId()
    {
        // Arrange
        DateTime firstStart = NowUtc.AddMinutes(-60);
        _timeline.Publish(
            [
                new ClipWindow(Guid.NewGuid().ToString("D"), firstStart, 300),
                new ClipWindow(Guid.NewGuid().ToString("D"), firstStart.AddMinutes(30), 300),
            ],
            NowUtc.AddHours(-24),
            NowUtc);
        _state.SetCursor(firstStart.AddMinutes(10));

        // Act
        Guid? mediaItemId = CreateSelection().MediaItemId;

        // Assert
        Assert.Null(mediaItemId);
    }

    [Fact]
    public void ReviewSelection_NoClipsPublished_ReturnsNullMediaItemId()
    {
        // Arrange
        _state.SetCursor(NowUtc.AddMinutes(-30));

        // Act
        Guid? mediaItemId = CreateSelection().MediaItemId;

        // Assert
        Assert.Null(mediaItemId);
    }

    [Fact]
    public void ReviewSelection_ClipIdIsNotAGuid_ReturnsNullMediaItemId()
    {
        // Arrange
        PublishClip("not-a-guid", NowUtc.AddMinutes(-60), 600);
        _state.SetCursor(NowUtc.AddMinutes(-59));

        // Act
        Guid? mediaItemId = CreateSelection().MediaItemId;

        // Assert
        Assert.Null(mediaItemId);
    }

    [Fact]
    public void ReviewSelection_NoCameraSelected_ReturnsNullMediaItemIdAndDeviceId()
    {
        // Arrange
        _state.SetCameras([]);
        PublishClip(Guid.NewGuid().ToString("D"), NowUtc.AddMinutes(-60), 600);
        _state.SetCursor(NowUtc.AddMinutes(-59));

        // Act
        ReviewSelection selection = CreateSelection();

        // Assert
        Assert.Null(selection.MediaItemId);
        Assert.Null(selection.DeviceId);
    }

    [Fact]
    public void ReviewSelection_SelectedEvent_ReturnsItsId()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        _state.SelectEvent(eventId);

        // Act
        Guid? selectedEventId = CreateSelection().EventId;

        // Assert
        Assert.Equal(eventId, selectedEventId);
    }

    [Fact]
    public void ReviewSelection_NoEventSelected_ReturnsNullEventId()
    {
        // Act
        Guid? eventId = CreateSelection().EventId;

        // Assert
        Assert.Null(eventId);
    }

    [Fact]
    public void ReviewSelection_TwoCameras_DeviceIdIsThePrimaryCamera()
    {
        // Act
        Guid? deviceId = CreateSelection().DeviceId;

        // Assert
        Assert.Equal(_primary, deviceId);
    }

    [Fact]
    public void ReviewSelection_CursorSet_AtUtcIsTheCursor()
    {
        // Arrange
        DateTime cursor = NowUtc.AddMinutes(-45);
        _state.SetCursor(cursor);

        // Act
        DateTime? atUtc = CreateSelection().AtUtc;

        // Assert
        Assert.Equal(cursor, atUtc);
    }
}
