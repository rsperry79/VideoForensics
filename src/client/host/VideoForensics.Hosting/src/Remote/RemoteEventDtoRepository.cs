using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// Client-side <see cref="IEventRepository"/>. Calls the server through <see cref="RemoteEventRepository"/> (the existing
    /// HTTP client for /api/v1/events) and maps each result to its DTO, so Ui.Shared never sees the entity types.
    /// </summary>
    public sealed class RemoteEventDtoRepository(IEventWriteRepository remote) : IEventRepository
    {
        /// <inheritdoc />
        public async Task<EventDto?> GetAsync(Guid eventId, CancellationToken ct) =>
            (await remote.GetAsync(eventId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<EventDto> UpsertAsync(EventDto @event, CancellationToken ct) =>
            (await remote.UpsertAsync(@event.ToDomain(), ct)).ToDto();

        /// <inheritdoc />
        public Task DeleteAsync(Guid eventId, CancellationToken ct) => remote.DeleteAsync(eventId, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await remote.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await remote.ListByLocationAndDateRangeAsync(locationId, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await remote.ListByDeviceEventTypeAndDateRangeAsync(deviceId, eventType, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            (await remote.ListByLocationEventTypeAndDateRangeAsync(locationId, eventType, fromUtc, toUtc, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            remote.GetEventTypeSummaryAsync(locationId, fromUtc, toUtc, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct) =>
            (await remote.ListUnansweredOrFlaggedAsync(deviceId, ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken ct) =>
            (await remote.ListAsync(ct)).Select(e => e.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<PaginatedResultDto<EventDto>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            PaginatedResult<Event> page = await remote.ListPaginatedAsync(pageNumber, pageSize, ct);
            return new PaginatedResultDto<EventDto>(page.Items.Select(e => e.ToDto()).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
        }

        /// <inheritdoc />
        public async Task<EventPageDto> ListPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? excludeEventTypes, IReadOnlyCollection<string>? includeEventTypes, int skip, int take, CancellationToken ct)
        {
            EventPage page = await remote.ListPageByDevicesAndDateRangeAsync(deviceIds, fromUtc, toUtc, excludeEventTypes, includeEventTypes, skip, take, ct);
            return new EventPageDto(page.Items.Select(e => e.ToDto()).ToList(), page.TotalCount);
        }
    }
}
