namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>The fixed rules the review events list applies to the event query.</summary>
public static class ReviewEventFilters
{
    /// <summary>
    /// Event types the events list leaves out. "snapshot" is a still the Ring provider stores as an event row; stills are
    /// media, not provider events, so they stay on the Timeline tab only.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedEventTypes = ["snapshot"];

    /// <summary>Rows the events list asks for per page.</summary>
    public const int PageSize = 100;
}
