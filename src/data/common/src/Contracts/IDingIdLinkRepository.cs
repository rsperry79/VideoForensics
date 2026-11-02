namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Reads and writes the link between a doorbot history row and the Events row for its ding. A doorbot history id is
    /// the string ding id, so the download path matches on exact equality.
    /// </summary>
    public interface IDingIdLinkRepository
    {
        /// <summary>
        /// Returns the string ding ids of one device that a doorbot history id could match. The device is resolved first
        /// from its provider id; with no device, there are no candidates. Candidates come only from string-id feeds.
        /// </summary>
        /// <param name="doorbotId">The numeric Ring device id, which is also the doorbot id on location and video search rows.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<IReadOnlyList<string>> GetCandidateDingIdsAsync(long doorbotId, CancellationToken ct);

        /// <summary>Sets the Events link on the doorbot history row with the given numeric id. Does nothing when no row has that id.</summary>
        /// <param name="doorbotHistoryProviderEventId">The numeric doorbot history id, as text.</param>
        /// <param name="eventId">The Events row this doorbot row belongs to.</param>
        /// <param name="ct">Cancellation token.</param>
        Task LinkDoorbotHistoryEventAsync(string doorbotHistoryProviderEventId, Guid eventId, CancellationToken ct);
    }
}
