using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IEventDetailRepository"/> over <see cref="IEventDetailWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class EventDetailDtoRepository(IEventDetailWriteRepository details) : IEventDetailRepository
    {
        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDetailDto>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct) =>
            (await details.GetDetailsAsync(eventIds, ct)).Select(d => d.ToDto()).ToList();
    }
}
