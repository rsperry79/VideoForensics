using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Event entities.</summary>
    public class EventRepository : IEventRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<EventRepository> _logger;

        /// <summary>Initializes a new instance of the EventRepository.</summary>
        public EventRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<EventRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets an event by ID.</summary>
        public async Task<Event?> GetAsync(Guid eventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        }

        /// <summary>Gets an event by device ID and provider event ID.</summary>
        public async Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.FirstOrDefaultAsync(
                e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId, ct);
        }

        /// <summary>Upserts (inserts or updates) an event by device ID and provider event ID.</summary>
        public async Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var existing = await db.Events.FirstOrDefaultAsync(
                    e => e.DeviceId == @event.DeviceId && e.ProviderEventId == @event.ProviderEventId, ct);

                if (existing == null)
                {
                    db.Events.Add(@event);
                    _logger.LogInformation("Event inserted: {EventId} ({ProviderEventId})", @event.Id, @event.ProviderEventId);
                }
                else
                {
                    existing.EventType = @event.EventType;
                    existing.OccurredAtUtc = @event.OccurredAtUtc;
                    existing.SnapshotUrl = @event.SnapshotUrl;
                    existing.MetadataJson = @event.MetadataJson;
                    existing.DiscoveredAtUtc = @event.DiscoveredAtUtc;
                    db.Events.Update(existing);
                    _logger.LogInformation("Event upserted (updated): {EventId}", @event.Id);
                }

                await db.SaveChangesAsync(ct);
                return existing ?? @event;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting event: {ProviderEventId}", @event.ProviderEventId);
                throw;
            }
        }

        /// <summary>Lists events for a device within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists events for all devices in a location within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);
        }

        /// <summary>Lists events that are unanswered or flagged for a device.</summary>
        public async Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events
                .Where(e => e.DeviceId == deviceId)
                .ToListAsync(ct);
        }

        /// <summary>Lists all events.</summary>
        public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.ToListAsync(ct);
        }

        /// <summary>Deletes an event.</summary>
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                if (@event != null)
                {
                    db.Events.Remove(@event);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Event deleted: {EventId}", eventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event: {EventId}", eventId);
                throw;
            }
        }
    }
}
