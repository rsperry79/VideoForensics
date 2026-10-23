namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

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
    Gallery,

    /// <summary>Device x hour grid of event/snapshot markers, for gap-spotting across devices.</summary>
    DeviceTime
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
        return string.IsNullOrWhiteSpace(value)
            ? EvidenceView.Timeline
            : value.ToLowerInvariant() switch
            {
                "timeline" => EvidenceView.Timeline,
                "grid" => EvidenceView.Grid,
                "gallery" => EvidenceView.Gallery,
                "devicetime" => EvidenceView.DeviceTime,
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
        : this(loadResult, loadResult.Items)
    {
    }

    /// <summary>Creates the view model with the items to show, which may be fewer than the loaded items (for example after snapshot thinning).</summary>
    public EvidenceViewModel(EvidenceLoadResult loadResult, IReadOnlyList<EvidenceItem> items)
    {
        Items = items;
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
    /// Media IDs with a thumbnail among Items (images, plus videos for their first frame), up to max, in order.
    /// </summary>
    public IReadOnlyList<Guid> ThumbnailMediaIds(int max = 500)
    {
        var ids = new List<Guid>();
        foreach (EvidenceItem item in Items)
        {
            if (ids.Count >= max)
            {
                break;
            }

            if (item.Media is not null && (MediaFormatHelper.IsImage(item.Media.MediaFormat) || MediaFormatHelper.IsVideo(item.Media.MediaFormat)))
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
        EvidenceItem? item = Items.FirstOrDefault(i => i.Key == key);
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

        (EvidenceItem _, EvidenceItem? next) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
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

        (EvidenceItem? previous, EvidenceItem _) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
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

            (EvidenceItem _, EvidenceItem? next) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
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

            (EvidenceItem? previous, EvidenceItem _) = EvidenceTimeline.Neighbours(Items, Selected.Key, viewableOnly: true);
            return previous is not null;
        }
    }
}
