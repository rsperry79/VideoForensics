using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// One page of media items plus the total number of matches, so a client can page without fetching every row.
    /// </summary>
    /// <param name="Items">The items on this page, in the order the query sorted them.</param>
    /// <param name="TotalCount">How many items match the query across all pages.</param>
    public sealed record MediaItemPage(IReadOnlyList<MediaItem> Items, int TotalCount);
}
