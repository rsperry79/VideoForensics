namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Read-only access to the detection child rows behind events (the Detail tab's data).</summary>
    public interface IEventDetailRepository
    {
        /// <summary>
        /// Returns one <see cref="EventDetail"/> per requested event that has any detail rows; events with none are omitted.
        /// Reads are batched into one query per table, so the cost does not grow with the number of events.
        /// <paramref name="eventIds"/> may be empty (returns empty) and must hold at most 200 ids.
        /// </summary>
        Task<IReadOnlyList<EventDetail>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct);
    }
}
