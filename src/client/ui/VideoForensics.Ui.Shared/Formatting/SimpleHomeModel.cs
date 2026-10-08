namespace VideoForensics.Ui.Shared.Formatting;

/// <summary>Kind of entry shown on the Simple Mode timeline.</summary>
public enum SimpleEntryKind
{
    /// <summary>A camera detected something (motion, person, package).</summary>
    Event,

    /// <summary>A camera was blocked or its signal was interfered with.</summary>
    Blocked
}

/// <summary>One plain-language line on the Simple Mode timeline.</summary>
/// <param name="TimeUtc">When it happened, in UTC.</param>
/// <param name="TimeText">Local time of day, such as "2:35 PM".</param>
/// <param name="Text">Plain-language description.</param>
/// <param name="Kind">Whether this is a detection or a blocked-camera entry.</param>
public sealed record SimpleTimelineEntry(DateTime TimeUtc, string TimeText, string Text, SimpleEntryKind Kind);

/// <summary>A day's worth of timeline entries, newest first.</summary>
/// <param name="Date">Local calendar date.</param>
/// <param name="Heading">"Today", "Yesterday" or a dated heading such as "Monday 6 October".</param>
/// <param name="Entries">Entries for the day, newest first.</param>
public sealed record SimpleTimelineDay(DateOnly Date, string Heading, IReadOnlyList<SimpleTimelineEntry> Entries);

/// <summary>A piece of evidence (video or snapshot) in plain language.</summary>
/// <param name="Label">Camera name and local time, such as "Front door camera - 2:35 PM".</param>
/// <param name="TypeText">"Video" or "Snapshot".</param>
/// <param name="TimeUtc">When it was recorded, in UTC.</param>
/// <param name="TimeText">Local time of day.</param>
/// <param name="Url">Browser-loadable address, or null when the item is not available.</param>
/// <param name="IsAvailable">False when the item can no longer be opened.</param>
public sealed record SimpleEvidenceItem(string Label, string TypeText, DateTime TimeUtc, string TimeText, string? Url, bool IsAvailable);

/// <summary>View model for the Simple Mode home: timeline plus evidence list.</summary>
/// <param name="Days">Timeline grouped by local day, newest first.</param>
/// <param name="Evidence">Evidence items, newest first.</param>
/// <param name="HasMoreTimeline">True when older entries were left out by the cap.</param>
/// <param name="WindowStartUtc">Start of the time window that was queried.</param>
public sealed record SimpleHomeModel(
    IReadOnlyList<SimpleTimelineDay> Days,
    IReadOnlyList<SimpleEvidenceItem> Evidence,
    bool HasMoreTimeline,
    DateTime WindowStartUtc);