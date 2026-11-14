namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Event reads, with DTO signatures, plus the upsert and delete commands the client uses. Client hosts use this
    /// interface. Backfill queues, dedup lookups and bulk maintenance are server-only (IEventWriteRepository).
    /// </summary>
    public interface IEventRepository
    {
        /// <summary>Gets an event by id, or null.</summary>
        Task<EventDto?> GetAsync(Guid eventId, CancellationToken ct);

        /// <summary>Creates or updates an event, returning the stored event.</summary>
        Task<EventDto> UpsertAsync(EventDto @event, CancellationToken ct);

        /// <summary>Deletes an event.</summary>
        Task DeleteAsync(Guid eventId, CancellationToken ct);

        /// <summary>Events for a device within a UTC range.</summary>
        Task<IReadOnlyList<EventDto>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Events for a location within a UTC range.</summary>
        Task<IReadOnlyList<EventDto>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Events of one type for a device within a UTC range.</summary>
        Task<IReadOnlyList<EventDto>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Events of one type for a location within a UTC range.</summary>
        Task<IReadOnlyList<EventDto>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Event counts by type for a location within a UTC range.</summary>
        Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Events for a device that are unanswered or flagged.</summary>
        Task<IReadOnlyList<EventDto>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all events.</summary>
        Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken ct);

        /// <summary>One page of events, with the total count.</summary>
        Task<PaginatedResultDto<EventDto>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct);

        /// <summary>Pages events from any of the given devices within a UTC range, with optional event-type filters.</summary>
        Task<EventPageDto> ListPageByDevicesAndDateRangeAsync(IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? excludeEventTypes, IReadOnlyCollection<string>? includeEventTypes, int skip, int take, CancellationToken ct);
    }
}
