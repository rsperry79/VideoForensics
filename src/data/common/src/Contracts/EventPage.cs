using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// One page of events plus the total number of matches, so the review page can page without fetching every event.
    /// </summary>
    /// <param name="Items">The events on this page, in the order the query sorted them (newest first).</param>
    /// <param name="TotalCount">How many events match the query across all pages.</param>
    public sealed record EventPage(IReadOnlyList<Event> Items, int TotalCount);
}
