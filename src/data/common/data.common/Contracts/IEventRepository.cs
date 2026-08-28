using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for event entities (independent of download status).</summary>
    public interface IEventRepository
    {
        /// <summary>Gets an event by ID.</summary>
        Task<Event?> GetAsync(Guid eventId, CancellationToken ct);

        /// <summary>Gets an event by device ID and provider event ID.</summary>
        Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct);

        /// <summary>Upserts (inserts or updates) an event by device ID and provider event ID.</summary>
        Task<Event> UpsertAsync(Event @event, CancellationToken ct);

        /// <summary>Lists events for a device within a date range.</summary>
        Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Lists events for all devices in a location within a date range.</summary>
        Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Lists events by type for a device within a date range.</summary>
        Task<IReadOnlyList<Event>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Lists events by type for all devices in a location within a date range.</summary>
        Task<IReadOnlyList<Event>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets event type summary (count by type) for a location within a date range.</summary>
        Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Lists events that are unanswered or flagged for a device.</summary>
        Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all events.</summary>
        Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct);

        /// <summary>Deletes an event.</summary>
        Task DeleteAsync(Guid eventId, CancellationToken ct);
    }
}
