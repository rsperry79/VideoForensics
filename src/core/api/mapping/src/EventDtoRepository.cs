using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Server-side <see cref="IEventRepository"/> that reads and writes events through <see cref="IEventWriteRepository"/>
    /// and maps each entity, page and result to its DTO.
    /// </summary>
    public sealed class EventDtoRepository(IEventWriteRepository events) : IEventRepository
    {
        /// <inheritdoc />
        public async Task<EventDto?> GetAsync(Guid eventId, CancellationToken ct) =>
            (await events.GetAsync(eventId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<EventDto> UpsertAsync(EventDto @event, CancellationToken ct) =>
            (await events.UpsertAsync(@event.ToDomain(), ct)).ToDto();

        /// <inheritdoc />
        public Task DeleteAsync(Guid eventId, CancellationToken ct) => events.DeleteAsync(eventId, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await events.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await events.ListByLocationAndDateRangeAsync(locationId, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await events.ListByDeviceEventTypeAndDateRangeAsync(deviceId, eventType, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await events.ListByLocationEventTypeAndDateRangeAsync(locationId, eventType, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            events.GetEventTypeSummaryAsync(locationId, fromUtc, toUtc, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct) =>
            (await events.ListUnansweredOrFlaggedAsync(deviceId, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken ct) =>
            (await events.ListAsync(ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<PaginatedResultDto<EventDto>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            PaginatedResult<Event> page = await events.ListPaginatedAsync(pageNumber, pageSize, ct);
            return new PaginatedResultDto<EventDto>(
                page.Items.Select(e => e.ToDto()).ToList(),
                page.TotalCount,
                page.PageNumber,
                page.PageSize);
        }

        /// <inheritdoc />
        public async Task<EventPageDto> ListPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? excludeEventTypes, IReadOnlyCollection<string>? includeEventTypes, int skip, int take, CancellationToken ct)
        {
            EventPage page = await events.ListPageByDevicesAndDateRangeAsync(deviceIds, fromUtc, toUtc, excludeEventTypes, includeEventTypes, skip, take, ct);
            return new EventPageDto(page.Items.Select(e => e.ToDto()).ToList(), page.TotalCount);
        }
    }
}
