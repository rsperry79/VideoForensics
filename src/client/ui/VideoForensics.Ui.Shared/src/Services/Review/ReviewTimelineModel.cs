using VideoForensics.Client.Common.Viewer;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The clips of the primary selected camera over the review range, held in memory so the right-panel timeline can draw
/// them on every tab. The timeline tab publishes after each successful load and clears when it has nothing to show.
/// </summary>
/// <remarks>
/// Only the in-memory copy is shared: a circuit restart empties it, and the timeline tab reloads it on the next render.
/// <see cref="OnChange"/> fires only when the content actually changes, so a reload that returns the same clips does not redraw the panel.
/// </remarks>
public sealed class ReviewTimelineModel
{
    private IReadOnlyList<ClipWindow> _clips = [];

    /// <summary>Raised once after a publish or clear changes the content.</summary>
    public event Action? OnChange;

    /// <summary>The clips of the primary camera, oldest first, as last published.</summary>
    public IReadOnlyList<ClipWindow> Clips => _clips;

    /// <summary>Start of the range the clips were loaded for (UTC).</summary>
    public DateTime FromUtc { get; private set; }

    /// <summary>End of the range the clips were loaded for (UTC).</summary>
    public DateTime ToUtc { get; private set; }

    /// <summary>
    /// Replaces the published clips and range. The clip list is copied, so the caller may reuse its list. Raises
    /// <see cref="OnChange"/> only when the clips or the range differ from what is already published.
    /// </summary>
    /// <exception cref="ArgumentException">The range is empty or reversed.</exception>
    public void Publish(IReadOnlyList<ClipWindow> clips, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(clips);
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("The timeline range must end after it starts.");
        }

        List<ClipWindow> copy = clips.ToList();
        bool changed = fromUtc != FromUtc || toUtc != ToUtc || !_clips.SequenceEqual(copy);
        if (!changed)
        {
            return;
        }

        _clips = copy;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        OnChange?.Invoke();
    }

    /// <summary>Removes the published clips. Raises <see cref="OnChange"/> only when there were clips to remove.</summary>
    public void Clear()
    {
        if (_clips.Count == 0)
        {
            return;
        }

        _clips = [];
        OnChange?.Invoke();
    }
}
