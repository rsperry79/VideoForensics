namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Read-only access to the detection child rows behind events, with DTO signatures. Client hosts use this interface.</summary>
    public interface IEventDetailRepository
    {
        /// <summary>
        /// Returns one <see cref="EventDetailDto"/> per requested event that has any detail rows; events with none are omitted.
        /// <paramref name="eventIds"/> may be empty (returns empty) and must hold at most 200 ids.
        /// </summary>
        Task<IReadOnlyList<EventDetailDto>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct);
    }
}
