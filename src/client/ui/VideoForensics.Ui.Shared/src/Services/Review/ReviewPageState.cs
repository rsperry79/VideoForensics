using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Services.Time;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The review page's single state object: cameras, time range, cursor, tab, filters, and the selected event.
/// Raises <see cref="OnChange"/> once per mutation that actually changes something, so the page re-renders
/// only when its state moves.
/// </summary>
/// <remarks>
/// The cursor is the one time every part of the page reads (player, strip, grid, tabs). It is kept inside the
/// range so a range move never leaves the player pointing at a time outside the window.
/// </remarks>
public sealed class ReviewPageState
{
    private readonly TimeProvider _timeProvider;
    private IReadOnlyList<Guid> _cameraIds = [];

    /// <summary>Raised once after a mutation changes state.</summary>
    public event Action? OnChange;

    /// <summary>Creates the state with the default range: the last 24 hours ending now.</summary>
    public ReviewPageState(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        ToUtc = _timeProvider.GetUtcNow().UtcDateTime;
        FromUtc = ToUtc.AddHours(-24);
    }

    /// <summary>Cameras shown on the page, in display order. At most <see cref="CameraSync.MaxTiles"/>.</summary>
    public IReadOnlyList<Guid> CameraIds => _cameraIds;

    /// <summary>
    /// The first camera of the most recent non-empty camera set. It outlives a later empty selection, so the Live link still
    /// opens the camera that was last on screen.
    /// </summary>
    public Guid? LastShownCameraId { get; private set; }

    /// <summary>Start of the review window (UTC).</summary>
    public DateTime FromUtc { get; private set; }

    /// <summary>End of the review window (UTC).</summary>
    public DateTime ToUtc { get; private set; }

    /// <summary>The instant the page is looking at (UTC), always inside the range, or null when unset.</summary>
    public DateTime? Cursor { get; private set; }

    /// <summary>The active tab.</summary>
    public ReviewTab ActiveTab { get; private set; } = ReviewTab.Timeline;

    /// <summary>Provider filter, or null for all providers.</summary>
    public string? ProviderFilter { get; private set; }

    /// <summary>Account filter, or null for all accounts.</summary>
    public Guid? AccountFilter { get; private set; }

    /// <summary>Location filter, or null for all locations.</summary>
    public Guid? LocationFilter { get; private set; }

    /// <summary>The event the user selected, or null.</summary>
    public Guid? SelectedEventId { get; private set; }

    /// <summary>
    /// Replaces the camera list. Duplicates are dropped (first occurrence wins) before the count is checked,
    /// so a repeated id never pushes a valid list over the limit.
    /// </summary>
    /// <exception cref="ArgumentException">More than <see cref="CameraSync.MaxTiles"/> distinct cameras were given.</exception>
    public void SetCameras(IEnumerable<Guid> cameraIds)
    {
        if (SetCamerasCore(cameraIds))
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Sets the review window and re-clamps the cursor into it. Times are normalised to UTC first; an unspecified
    /// kind is read as UTC.
    /// </summary>
    /// <exception cref="ArgumentException">The window is empty or reversed, or a time is not UTC after normalising.</exception>
    public void SetRange(DateTime fromUtc, DateTime toUtc)
    {
        if (SetRangeCore(fromUtc, toUtc))
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Sets the cursor, clamped into the range. Null clears it.</summary>
    public void SetCursor(DateTime? utc)
    {
        if (SetCursorCore(utc))
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Switches the active tab.</summary>
    public void SetTab(ReviewTab tab)
    {
        if (ActiveTab == tab)
        {
            return;
        }

        ActiveTab = tab;
        OnChange?.Invoke();
    }

    /// <summary>Sets the provider, account and location filters. A blank provider is stored as null.</summary>
    public void SetFilters(string? provider, Guid? account, Guid? location = null)
    {
        if (SetFiltersCore(provider, account, location))
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Selects an event. Selecting never moves the cursor; the caller seeks when it wants to.</summary>
    public void SelectEvent(Guid? id)
    {
        if (SelectedEventId == id)
        {
            return;
        }

        SelectedEventId = id;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Applies a global investigation scope: its devices (distinct, capped at the camera limit, order kept) and,
    /// when the scope has a non-empty range, its range. One <see cref="OnChange"/> for the whole call.
    /// </summary>
    public void ApplyScope(ForensicScope scope)
    {
        bool changed = SetCamerasCore(scope.DeviceIds.Distinct().Take(CameraSync.MaxTiles));

        DateTime from = UserTimeZone.AsUtc(scope.FromUtc);
        DateTime to = UserTimeZone.AsUtc(scope.ToUtc);
        if (from < to)
        {
            changed |= SetRangeCore(from, to);
        }

        if (changed)
        {
            OnChange?.Invoke();
        }
    }

    private bool SetCamerasCore(IEnumerable<Guid> cameraIds)
    {
        List<Guid> distinct = cameraIds.Distinct().ToList();
        if (distinct.Count > CameraSync.MaxTiles)
        {
            throw new ArgumentException($"At most {CameraSync.MaxTiles} cameras can be shown at once.", nameof(cameraIds));
        }

        if (_cameraIds.SequenceEqual(distinct))
        {
            return false;
        }

        _cameraIds = distinct;
        if (distinct.Count > 0)
        {
            LastShownCameraId = distinct[0];
        }

        return true;
    }

    private bool SetRangeCore(DateTime fromUtc, DateTime toUtc)
    {
        DateTime from = UserTimeZone.AsUtc(fromUtc);
        DateTime to = UserTimeZone.AsUtc(toUtc);
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The review range must be UTC.");
        }

        if (from >= to)
        {
            throw new ArgumentException("The review range must start before it ends.");
        }

        bool changed = from != FromUtc || to != ToUtc;
        FromUtc = from;
        ToUtc = to;

        // A range move can leave the cursor outside the window, so pull it back in.
        bool cursorMoved = SetCursorCore(Cursor);
        return changed || cursorMoved;
    }

    private bool SetCursorCore(DateTime? utc)
    {
        DateTime? next = null;
        if (utc is not null)
        {
            DateTime value = UserTimeZone.AsUtc(utc.Value);
            next = value < FromUtc ? FromUtc : value > ToUtc ? ToUtc : value;
        }

        if (next == Cursor)
        {
            return false;
        }

        Cursor = next;
        return true;
    }

    private bool SetFiltersCore(string? provider, Guid? account, Guid? location)
    {
        string? normalizedProvider = string.IsNullOrWhiteSpace(provider) ? null : provider;
        if (normalizedProvider == ProviderFilter && account == AccountFilter && location == LocationFilter)
        {
            return false;
        }

        ProviderFilter = normalizedProvider;
        AccountFilter = account;
        LocationFilter = location;
        return true;
    }
}
