namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>The fixed rules the review events list applies to the event query.</summary>
public static class ReviewEventFilters
{
    /// <summary>
    /// Event types the events list leaves out. "snapshot" is a still the Ring provider stores as an event row; stills are
    /// media, not provider events, so they stay on the Timeline tab only.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedEventTypes = ["snapshot"];

    /// <summary>The provider event types the Event Filter offers, in display order. Each has an <c>EventType_&lt;type&gt;</c> label.</summary>
    public static readonly IReadOnlyList<string> KnownEventTypes = ["motion", "ding", "doorbell", "on_demand", "alarm", "person"];

    /// <summary>The system type for signal anomalies (the Detail tab's Anomalies section).</summary>
    public const string SignalAnomaly = "signal_anomaly";

    /// <summary>The system type for jamming incidents (the Detail tab's Jamming section).</summary>
    public const string Jamming = "jamming";

    /// <summary>System types the Event Filter offers after the provider types. They come from signal analysis, not the provider.</summary>
    public static readonly IReadOnlyList<string> SystemEventTypes = [SignalAnomaly, Jamming];

    /// <summary>Every type the Event Filter offers, provider types first, in display order.</summary>
    public static readonly IReadOnlyList<string> SelectableEventTypes = [.. KnownEventTypes, .. SystemEventTypes];

    /// <summary>
    /// The provider event types a selection asks for, for the events query. Null means every type (nothing selected);
    /// an empty list means the selection holds only system types, so no provider events match.
    /// </summary>
    public static IReadOnlyList<string>? ProviderTypes(IReadOnlyCollection<string> selection)
    {
        return selection.Count == 0 ? null : selection.Where(KnownEventTypes.Contains).ToList();
    }

    /// <summary>True when a list of <paramref name="type"/> shows under <paramref name="selection"/>: nothing selected, or the type is in it.</summary>
    public static bool Shows(IReadOnlyCollection<string> selection, string type)
    {
        return selection.Count == 0 || selection.Contains(type);
    }

    /// <summary>Keeps the known types, once each, in display order. Anything else is dropped.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? types)
    {
        HashSet<string> wanted = (types ?? []).ToHashSet();
        return SelectableEventTypes.Where(wanted.Contains).ToList();
    }

    /// <summary>Rows the events list
    /// <summary>Rows the events list asks for per page.</summary>
    public const int PageSize = 100;
}
