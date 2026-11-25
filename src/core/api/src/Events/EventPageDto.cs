namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// One page of events for the review page's Events tab. <see cref="TotalCount"/> counts every match, not just this page.
    /// </summary>
    /// <param name="Items">The events on this page, newest first.</param>
    /// <param name="TotalCount">How many events match the query across all pages.</param>
    public sealed record EventPageDto(IReadOnlyList<EventDto> Items, int TotalCount);
}
