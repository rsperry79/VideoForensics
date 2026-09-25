namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Enumeration of evidence view types.
/// </summary>
public enum EvidenceView
{
    /// <summary>Timeline view with day and device grouping.</summary>
    Timeline,

    /// <summary>Grid view with sortable columns.</summary>
    Grid,

    /// <summary>Gallery view with image/video tiles.</summary>
    Gallery
}

/// <summary>
/// Pure view model for the Evidence page, managing loaded items, selection, and viewer state.
/// </summary>
public class EvidenceViewModel
{
    /// <summary>
    /// Parse a view name string to an EvidenceView, defaulting to Timeline if not recognized.
    /// </summary>
    public static EvidenceView ParseView(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EvidenceView.Timeline;
        }

        return value.ToLowerInvariant() switch
        {
            "timeline" => EvidenceView.Timeline,
            "grid" => EvidenceView.Grid,
            "gallery" => EvidenceView.Gallery,
            _ => EvidenceView.Timeline
        };
    }

    /// <summary>
    /// All loaded evidence items.
    /// </summary>
    public IReadOnlyList<EvidenceItem> Items { get; }

    /// <summary>
    /// Currently selected item, or null if none selected.
    /// </summary>
    public EvidenceItem? Selected { get; private set; }

    /// <summary>
    /// True if the media viewer is currently open (hiding the main view).
    /// </summary>
    public bool IsViewerOpen { get; private set; }

    /// <summary>
    /// The load result containing errors and holds.
    /// </summary>
    public EvidenceLoadResult LoadResult { get; }

    public EvidenceViewModel(EvidenceLoadResult loadResult)
    {
        Items = loadResult.Items;
        LoadResult = loadResult;
        Selected = null;
        IsViewerOpen = false;
    }

    /// <summary>
    /// Items with viewable media (images or videos) for gallery view.
    /// </summary>
    public IReadOnlyList<EvidenceItem> GalleryItems =>
        Items.Where(i => i.HasViewableMedia).ToList().AsReadOnly();

    /// <summary>
    /// Media IDs for images among Items (up to max, in order).
    /// </summary>
    public IReadOnlyList<Guid> ThumbnailMediaIds(int max = 500)
    {
        var ids = new List<Guid>();
        foreach (var item in Items)
        {
            if (ids.Count >= max)
            {
                break;
            }

            if (item.Media is not null && MediaFormatHelper.IsImage(item.Media.MediaFormat))
            {
                ids.Add(item.Media.Id);
            }
        }

        return ids.AsReadOnly();
    }

    /// <summary>
    /// Open/select an item by its key.
    /// </summary>
    public void Open(string key)
    {
        var item = Items.FirstOrDefault(i => i.Key == key);
        if (item is not null)
        {
            Selected = item;
            IsViewerOpen = true;
        }
    }

    /// <summary>
    /// Close the viewer, clearing the selected item.
    /// </summary>
    public void Close()
    {
        Selected = null;
        IsViewerOpen = false;
    }

    /// <summary>
    /// Move to the next viewable item in the sequence.
    /// </summary>
    public void MoveNext()
    {
        if (Selected is null)
        {
            return;
        }

        var (_, next) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
        if (next is not null)
        {
            Selected = next;
        }
    }

    /// <summary>
    /// Move to the previous viewable item in the sequence.
    /// </summary>
    public void MovePrevious()
    {
        if (Selected is null)
        {
            return;
        }

        var (previous, _) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
        if (previous is not null)
        {
            Selected = previous;
        }
    }

    /// <summary>
    /// True if there is a next viewable item from the selected item.
    /// </summary>
    public bool HasNext
    {
        get
        {
            if (Selected is null)
            {
                return false;
            }

            var (_, next) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
            return next is not null;
        }
    }

    /// <summary>
    /// True if there is a previous viewable item from the selected item.
    /// </summary>
    public bool HasPrevious
    {
        get
        {
            if (Selected is null)
            {
                return false;
            }

            var (previous, _) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
            return previous is not null;
        }
    }
}
