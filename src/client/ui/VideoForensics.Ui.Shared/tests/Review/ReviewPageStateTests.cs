using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

public class ReviewPageState_Tests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private static ReviewPageState NewState()
    {
        return new ReviewPageState(new FakeTimeProvider(Now));
    }

    private static List<Guid> NewIds(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }

    [Fact]
    public void LastShownCameraId_NothingShownYet_IsNull()
    {
        Assert.Null(NewState().LastShownCameraId);
    }

    [Fact]
    public void LastShownCameraId_CamerasSet_IsTheFirstCamera()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> ids = NewIds(3);

        // Act
        state.SetCameras(ids);

        // Assert
        Assert.Equal(ids[0], state.LastShownCameraId);
    }

    [Fact]
    public void LastShownCameraId_CamerasLaterCleared_KeepsTheLastShownCamera()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> ids = NewIds(2);
        state.SetCameras(ids);

        // Act
        state.SetCameras([]);

        // Assert
        Assert.Equal(ids[0], state.LastShownCameraId);
    }

    [Fact]
    public void Constructor_NoRangeGiven_SpansLast24HoursEndingNow()
    {
        // Arrange and Act
        ReviewPageState state = NewState();

        // Assert
        Assert.Equal(Now, state.ToUtc);
        Assert.Equal(Now.AddHours(-24), state.FromUtc);
        Assert.Null(state.Cursor);
        Assert.Equal(ReviewTab.Timeline, state.ActiveTab);
        Assert.Empty(state.CameraIds);
    }

    [Fact]
    public void SetCameras_TenDistinctCameras_ThrowsArgumentException()
    {
        // Arrange
        ReviewPageState state = NewState();

        // Act and Assert
        Assert.Throws<ArgumentException>(() => state.SetCameras(NewIds(CameraSync.MaxTiles + 1)));
    }

    [Fact]
    public void SetCameras_TenthCamera_LeavesCamerasUnchangedAndRaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> nine = NewIds(CameraSync.MaxTiles);
        state.SetCameras(nine);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        Assert.Throws<ArgumentException>(() => state.SetCameras(NewIds(CameraSync.MaxTiles + 1)));

        // Assert
        Assert.Equal(nine, state.CameraIds);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetCameras_NineDistinctCameras_Accepted()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> nine = NewIds(CameraSync.MaxTiles);

        // Act
        state.SetCameras(nine);

        // Assert
        Assert.Equal(nine, state.CameraIds);
    }

    [Fact]
    public void SetCameras_DuplicateIds_RemovedKeepingFirstOrder()
    {
        // Arrange
        ReviewPageState state = NewState();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        // Act
        state.SetCameras([second, first, second, first]);

        // Assert
        Assert.Equal([second, first], state.CameraIds);
    }

    [Fact]
    public void SetCameras_DuplicatesInflateCountPastNine_AcceptedAfterDedupe()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> nine = NewIds(CameraSync.MaxTiles);
        var withDuplicates = nine.Concat(nine).ToList();

        // Act
        state.SetCameras(withDuplicates);

        // Assert
        Assert.Equal(nine, state.CameraIds);
    }

    [Fact]
    public void SetCameras_SameListAgain_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> ids = NewIds(2);
        state.SetCameras(ids);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetCameras(ids);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetCameras_NewList_RaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetCameras(NewIds(3));

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetRange_CursorBeforeNewFrom_ClampsCursorToFrom()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetCursor(Now.AddHours(-2));
        DateTime newFrom = Now.AddHours(-1);

        // Act
        state.SetRange(newFrom, Now);

        // Assert
        Assert.Equal(newFrom, state.Cursor);
    }

    [Fact]
    public void SetRange_CursorBeyondNewTo_ClampsCursorToTo()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetCursor(Now.AddHours(-1));
        DateTime newTo = Now.AddHours(-2);

        // Act
        state.SetRange(Now.AddHours(-10), newTo);

        // Assert
        Assert.Equal(newTo, state.Cursor);
    }

    [Fact]
    public void SetRange_CursorInsideNewRange_Unchanged()
    {
        // Arrange
        ReviewPageState state = NewState();
        DateTime cursor = Now.AddHours(-3);
        state.SetCursor(cursor);

        // Act
        state.SetRange(Now.AddHours(-10), Now);

        // Assert
        Assert.Equal(cursor, state.Cursor);
    }

    [Fact]
    public void SetRange_NoCursor_StaysNull()
    {
        // Arrange
        ReviewPageState state = NewState();

        // Act
        state.SetRange(Now.AddHours(-1), Now);

        // Assert
        Assert.Null(state.Cursor);
    }

    [Fact]
    public void SetRange_FromNotBeforeTo_ThrowsArgumentException()
    {
        // Arrange
        ReviewPageState state = NewState();

        // Act and Assert
        Assert.Throws<ArgumentException>(() => state.SetRange(Now, Now));
    }

    [Fact]
    public void SetRange_UnspecifiedKind_TreatedAsUtc()
    {
        // Arrange
        ReviewPageState state = NewState();
        var from = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Unspecified);
        var to = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        state.SetRange(from, to);

        // Assert
        Assert.Equal(DateTimeKind.Utc, state.FromUtc.Kind);
        Assert.Equal(new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc), state.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc), state.ToUtc);
    }

    [Fact]
    public void SetRange_SameRange_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetRange(Now.AddHours(-24), Now);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetRange_NewRange_RaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetCursor(Now.AddHours(-2));
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetRange(Now.AddHours(-1), Now);

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetCursor_BeforeFrom_ClampsToFrom()
    {
        // Arrange
        ReviewPageState state = NewState();

        // Act
        state.SetCursor(Now.AddDays(-3));

        // Assert
        Assert.Equal(state.FromUtc, state.Cursor);
    }

    [Fact]
    public void SetCursor_AfterTo_ClampsToTo()
    {
        // Arrange
        ReviewPageState state = NewState();

        // Act
        state.SetCursor(Now.AddDays(3));

        // Assert
        Assert.Equal(state.ToUtc, state.Cursor);
    }

    [Fact]
    public void SetCursor_Null_ClearsCursor()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetCursor(Now.AddHours(-1));

        // Act
        state.SetCursor(null);

        // Assert
        Assert.Null(state.Cursor);
    }

    [Fact]
    public void SetCursor_SameValue_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        DateTime cursor = Now.AddHours(-1);
        state.SetCursor(cursor);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetCursor(cursor);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetCursor_NewValue_RaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetCursor(Now.AddHours(-1));

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetTab_NewTab_RaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetTab(ReviewTab.Events);

        // Assert
        Assert.Equal(ReviewTab.Events, state.ActiveTab);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetTab_SameTab_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetTab(ReviewTab.Timeline);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetFilters_BlankProvider_BecomesNull()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetFilters("Ring", null);

        // Act
        state.SetFilters("   ", null);

        // Assert
        Assert.Null(state.ProviderFilter);
    }

    [Fact]
    public void SetFilters_ProviderAndAccount_Stored()
    {
        // Arrange
        ReviewPageState state = NewState();
        Guid account = Guid.NewGuid();

        // Act
        state.SetFilters("Ring", account);

        // Assert
        Assert.Equal("Ring", state.ProviderFilter);
        Assert.Equal(account, state.AccountFilter);
    }

    [Fact]
    public void SetFilters_SameValues_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetFilters("Ring", null);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetFilters("Ring", null);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetFilters_BlankProviderOnDefault_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetFilters("  ", null);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetFilters_Location_Stored()
    {
        // Arrange
        ReviewPageState state = NewState();
        Guid location = Guid.NewGuid();

        // Act
        state.SetFilters(null, null, location);

        // Assert
        Assert.Equal(location, state.LocationFilter);
    }

    [Fact]
    public void SetFilters_LocationChanged_RaisesOnChangeOnce_AndSameValueRaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        Guid location = Guid.NewGuid();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SetFilters(null, null, location);
        state.SetFilters(null, null, location);

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SelectEvent_NewId_RaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SelectEvent(Guid.NewGuid());

        // Assert
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SelectEvent_SameId_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        Guid id = Guid.NewGuid();
        state.SelectEvent(id);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.SelectEvent(id);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SelectEvent_NewId_DoesNotMoveCursor()
    {
        // Arrange
        ReviewPageState state = NewState();
        DateTime cursor = Now.AddHours(-1);
        state.SetCursor(cursor);

        // Act
        state.SelectEvent(Guid.NewGuid());

        // Assert
        Assert.Equal(cursor, state.Cursor);
    }

    [Fact]
    public void ApplyScope_DeviceIds_DistinctCappedAtNineKeepingOrder()
    {
        // Arrange
        ReviewPageState state = NewState();
        List<Guid> ids = NewIds(11);
        var deviceIds = ids.Concat([ids[0]]).ToList();
        var scope = new ForensicScope(deviceIds, Now.AddHours(-24), Now, null);

        // Act
        state.ApplyScope(scope);

        // Assert
        Assert.Equal(ids.Take(CameraSync.MaxTiles).ToList(), state.CameraIds);
    }

    [Fact]
    public void ApplyScope_WithRange_SetsRangeAndRaisesOnChangeOnce()
    {
        // Arrange
        ReviewPageState state = NewState();
        var from = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Unspecified);
        var to = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Unspecified);
        var scope = new ForensicScope([Guid.NewGuid()], from, to, null);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.ApplyScope(scope);

        // Assert
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), state.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc), state.ToUtc);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ApplyScope_EmptyRange_KeepsCurrentRange()
    {
        // Arrange
        ReviewPageState state = NewState();
        var scope = new ForensicScope([], Now, Now, null);

        // Act
        state.ApplyScope(scope);

        // Assert
        Assert.Equal(Now.AddHours(-24), state.FromUtc);
        Assert.Equal(Now, state.ToUtc);
    }

    [Fact]
    public void ApplyScope_NothingChanged_RaisesNothing()
    {
        // Arrange
        ReviewPageState state = NewState();
        var scope = new ForensicScope([], Now.AddHours(-24), Now, null);
        int raised = 0;
        state.OnChange += () => raised++;

        // Act
        state.ApplyScope(scope);

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetFilters_EventType_IsStoredAndRaisesOnChange()
    {
        // Arrange
        ReviewPageState state = NewState();
        int changes = 0;
        state.OnChange += () => changes++;

        // Act
        state.SetFilters(null, null, null, "motion");

        // Assert
        Assert.Equal("motion", state.EventTypeFilter);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SetFilters_BlankEventType_IsStoredAsNull()
    {
        // Arrange
        ReviewPageState state = NewState();
        state.SetFilters(null, null, null, "motion");

        // Act
        state.SetFilters(null, null, null, " ");

        // Assert
        Assert.Null(state.EventTypeFilter);
    }
}
