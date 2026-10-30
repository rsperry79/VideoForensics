using VideoForensics.Client.Common.Viewer;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The review page's current selection, read by the Actions menu: the clip of the primary camera that covers the
/// cursor, the selected event, the primary camera, and the cursor time.
/// </summary>
/// <remarks>
/// Every value is read live from its source on each access, so an action always acts on what the page shows at the
/// moment it runs, not on what was shown when the menu opened.
/// </remarks>
public sealed class ReviewSelection
{
    private readonly ReviewPageState _state;
    private readonly ReviewTimelineModel _timeline;

    /// <summary>Creates the selection over the page state and the published timeline clips.</summary>
    public ReviewSelection(ReviewPageState state, ReviewTimelineModel timeline)
    {
        _state = state;
        _timeline = timeline;
    }

    /// <summary>
    /// The media item id of the primary camera's clip that covers the cursor. Null when there is no cursor, no camera,
    /// no clip covers the cursor (a gap), or the clip id is not a guid.
    /// </summary>
    public Guid? MediaItemId
    {
        get
        {
            // The clips are the primary camera's only; with no camera selected, a stale clip list must not act.
            if (_state.Cursor is not DateTime cursor || _state.CameraIds.Count == 0)
            {
                return null;
            }

            ClipSelection? covering = ReviewTimeline.ClipAt(_timeline.Clips, cursor);
            if (covering is null)
            {
                return null;
            }

            return Guid.TryParse(covering.ClipId, out Guid mediaItemId) ? mediaItemId : null;
        }
    }

    /// <summary>The event the user selected on the events tab, or null.</summary>
    public Guid? EventId => _state.SelectedEventId;

    /// <summary>The primary camera (the first one shown), or null when no camera is selected.</summary>
    public Guid? DeviceId => _state.CameraIds.Count > 0 ? _state.CameraIds[0] : null;

    /// <summary>The cursor time (UTC), or null when the cursor is unset.</summary>
    public DateTime? AtUtc => _state.Cursor;
}
