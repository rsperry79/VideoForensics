using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The review page's case view. While a case is entered, the review loaders return only that case's pinned items,
/// so every tab and the right-panel timeline show the case without knowing about it.
/// </summary>
/// <remarks>
/// Scoped per circuit, like <see cref="ReviewPageState"/>. The pinned rows are loaded once on entry; a pin added
/// elsewhere while the case is open is not seen until the case is entered again.
/// </remarks>
public sealed class ReviewCaseMode
{
    /// <summary>
    /// Most pinned items of each kind the view loads. A larger case keeps the newest pins and sets <see cref="Truncated"/>,
    /// so one case cannot load an unbounded number of rows into the page.
    /// </summary>
    public const int MaxItemsPerKind = 500;

    /// <summary>Padding added before the earliest pinned start and after the latest pinned end.</summary>
    public static readonly TimeSpan RangePadding = TimeSpan.FromMinutes(1);

    private readonly ICaseRepository _cases;
    private readonly IMediaItemRepository _mediaItems;
    private readonly IEventRepository _events;

    private List<MediaItem> _mediaList = [];
    private List<Event> _eventList = [];
    private List<Guid> _deviceIds = [];

    /// <summary>Creates the case view over the case and row repositories. It starts inactive.</summary>
    public ReviewCaseMode(ICaseRepository cases, IMediaItemRepository mediaItems, IEventRepository events)
    {
        _cases = cases;
        _mediaItems = mediaItems;
        _events = events;
    }

    /// <summary>Raised once each time the view enters a case or leaves one.</summary>
    public event Action? OnChange;

    /// <summary>True while a case is entered.</summary>
    public bool Active { get; private set; }

    /// <summary>The entered case's id, or null when inactive.</summary>
    public Guid? CaseId { get; private set; }

    /// <summary>The entered case's title, or null when inactive.</summary>
    public string? CaseName { get; private set; }

    /// <summary>True when the case had more pins of one kind than <see cref="MaxItemsPerKind"/> and the oldest were left out.</summary>
    public bool Truncated { get; private set; }

    /// <summary>The ids of the pinned media items the view shows.</summary>
    public IReadOnlySet<Guid> MediaItemIds { get; private set; } = new HashSet<Guid>();

    /// <summary>The pinned media items, oldest recording first.</summary>
    public IReadOnlyList<MediaItem> MediaItems => _mediaList;

    /// <summary>The pinned events, newest first.</summary>
    public IReadOnlyList<Event> Events => _eventList;

    /// <summary>The distinct devices of the pinned items, in the order the case lists them.</summary>
    public IReadOnlyList<Guid> DeviceIds => _deviceIds;

    /// <summary>Start of the window that covers every pinned item, padded by <see cref="RangePadding"/>. Null when the case has no items.</summary>
    public DateTime? FromUtc { get; private set; }

    /// <summary>End of the window that covers every pinned item, padded by <see cref="RangePadding"/>. Null when the case has no items.</summary>
    public DateTime? ToUtc { get; private set; }

    /// <summary>The earliest pinned start (unpadded). The page places its cursor here. Null when the case has no items.</summary>
    public DateTime? EarliestItemUtc { get; private set; }

    /// <summary>
    /// Enters a case. Loads its active pins, then the rows they point at. Returns false, and leaves the view inactive,
    /// when the case does not exist. Entering again replaces the case already shown.
    /// </summary>
    /// <remarks>
    /// The cap keeps the pins added most recently, because that is the only time known before the rows are loaded.
    /// A pin whose row is gone (for example, purged) is skipped rather than failing the entry.
    /// </remarks>
    public async Task<bool> EnterAsync(Guid caseId, CancellationToken ct)
    {
        ForensicCase? forensicCase = await _cases.GetAsync(caseId, ct);
        if (forensicCase is null)
        {
            if (Clear())
            {
                OnChange?.Invoke();
            }

            return false;
        }

        IReadOnlyList<CaseItem> pins = await _cases.ListItemsAsync(caseId, false, ct);
        List<CaseItem> activePins = pins.Where(pin => pin.RemovedAtUtc is null).ToList();

        List<CaseItem> mediaPins = activePins.Where(pin => pin.Kind == CaseItemKind.Media && pin.MediaItemId is not null).ToList();
        List<CaseItem> eventPins = activePins.Where(pin => pin.Kind == CaseItemKind.Event && pin.EventId is not null).ToList();
        bool truncated = mediaPins.Count > MaxItemsPerKind || eventPins.Count > MaxItemsPerKind;

        HashSet<Guid> keptMedia = mediaPins.OrderByDescending(pin => pin.AddedAtUtc).Take(MaxItemsPerKind).Select(pin => pin.MediaItemId!.Value).ToHashSet();
        HashSet<Guid> keptEvents = eventPins.OrderByDescending(pin => pin.AddedAtUtc).Take(MaxItemsPerKind).Select(pin => pin.EventId!.Value).ToHashSet();

        var mediaById = new Dictionary<Guid, MediaItem>();
        foreach (Guid id in mediaPins.Select(pin => pin.MediaItemId!.Value).Where(keptMedia.Contains))
        {
            MediaItem? item = await _mediaItems.GetAsync(id, ct);
            if (item is not null)
            {
                mediaById[id] = item;
            }
        }

        var eventById = new Dictionary<Guid, Event>();
        foreach (Guid id in eventPins.Select(pin => pin.EventId!.Value).Where(keptEvents.Contains))
        {
            Event? evt = await _events.GetAsync(id, ct);
            if (evt is not null)
            {
                eventById[id] = evt;
            }
        }

        _ = Clear();
        Active = true;
        CaseId = caseId;
        CaseName = forensicCase.Title;
        Truncated = truncated;
        _mediaList = mediaById.Values.OrderBy(item => item.RecordedAtUtc).ThenBy(item => item.Id).ToList();
        _eventList = eventById.Values.OrderByDescending(evt => evt.OccurredAtUtc).ToList();
        MediaItemIds = mediaById.Keys.ToHashSet();
        _deviceIds = DistinctDevicesInPinOrder(activePins, mediaById, eventById);
        ComputeRange();
        OnChange?.Invoke();
        return true;
    }

    /// <summary>Leaves the case view and clears what it loaded. Does nothing when no case is entered.</summary>
    public void Exit()
    {
        if (Clear())
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Resets every field. Returns true when a case was entered before the reset.</summary>
    private bool Clear()
    {
        bool wasActive = Active;
        Active = false;
        CaseId = null;
        CaseName = null;
        Truncated = false;
        _mediaList = [];
        _eventList = [];
        _deviceIds = [];
        MediaItemIds = new HashSet<Guid>();
        FromUtc = null;
        ToUtc = null;
        EarliestItemUtc = null;
        return wasActive;
    }

    /// <summary>
    /// Walks the pins in the order the case lists them, so the first device a pin names comes first in the list.
    /// Only pins whose rows were loaded count, so a dropped pin never adds a device.
    /// </summary>
    private static List<Guid> DistinctDevicesInPinOrder(
        IEnumerable<CaseItem> pins,
        IReadOnlyDictionary<Guid, MediaItem> mediaById,
        IReadOnlyDictionary<Guid, Event> eventById)
    {
        var devices = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (CaseItem pin in pins)
        {
            Guid? device = pin.Kind == CaseItemKind.Media && pin.MediaItemId is Guid mediaId && mediaById.TryGetValue(mediaId, out MediaItem? item)
                ? item.DeviceId
                : pin.Kind == CaseItemKind.Event && pin.EventId is Guid eventId && eventById.TryGetValue(eventId, out Event? evt)
                    ? evt.DeviceId
                    : null;

            if (device is Guid id && seen.Add(id))
            {
                devices.Add(id);
            }
        }

        return devices;
    }

    /// <summary>Sets the window from the earliest start to the latest end of the loaded rows, padded on both sides.</summary>
    private void ComputeRange()
    {
        var starts = _mediaList.Select(item => item.RecordedAtUtc)
            .Concat(_eventList.Select(evt => evt.OccurredAtUtc))
            .ToList();
        if (starts.Count == 0)
        {
            return;
        }

        // A clip with no recorded duration is treated as ending where it starts, so the window never shrinks below its start.
        var ends = _mediaList.Select(item => item.RecordedAtUtc.AddSeconds(item.DurationSeconds ?? 0))
            .Concat(_eventList.Select(evt => evt.OccurredAtUtc));

        DateTime earliest = starts.Min();
        DateTime latest = ends.Max();
        EarliestItemUtc = earliest;
        FromUtc = earliest - RangePadding;
        ToUtc = latest + RangePadding;
    }
}
