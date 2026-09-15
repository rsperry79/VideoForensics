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
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        }

        /// <summary>Gets an event by device ID and provider event ID.</summary>
        public async Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking().FirstOrDefaultAsync(
                e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId, ct);
        }

        /// <summary>Gets an event by API source hash for deduplication.</summary>
        public async Task<Event?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.ApiSourceHash == apiSourceHash, ct);
        }

        /// <summary>Upserts (inserts or updates) an event by device ID and provider event ID.</summary>
        public async Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Event? existing = await db.Events.FirstOrDefaultAsync(
                    e => e.DeviceId == @event.DeviceId && e.ProviderEventId == @event.ProviderEventId, ct);

                if (existing == null)
                {
                    _ = db.Events.Add(@event);
                    _logger.LogInformation("Event inserted: {EventId} ({ProviderEventId})", @event.Id, @event.ProviderEventId);
                }
                else
                {
                    // Progressive enrichment: an event is first upserted when merely discovered
                    // (download-status fields still null) and later re-upserted once downloaded.
                    // Only overwrite the download-status fields when the incoming value is
                    // non-null, so a later "discovered" upsert for the same event can't wipe out
                    // an already-recorded download/hash.
                    existing.EventType = @event.EventType;
                    existing.OccurredAtUtc = @event.OccurredAtUtc;
                    existing.SnapshotUrl = @event.SnapshotUrl ?? existing.SnapshotUrl;
                    existing.MetadataJson = @event.MetadataJson ?? existing.MetadataJson;
                    existing.DiscoveredAtUtc = @event.DiscoveredAtUtc;
                    existing.DownloadedAtUtc = @event.DownloadedAtUtc ?? existing.DownloadedAtUtc;
                    existing.ApiSourceHash = @event.ApiSourceHash ?? existing.ApiSourceHash;
                    existing.EventIntegrityHash = @event.EventIntegrityHash ?? existing.EventIntegrityHash;

                    // Update DownloadStatus based on DownloadedAtUtc
                    if (existing.DownloadedAtUtc.HasValue)
                    {
                        existing.DownloadStatus = EventDownloadStatus.Downloaded;
                    }

                    _ = db.Events.Update(existing);
                    _logger.LogInformation("Event upserted (updated): {EventId}", @event.Id);
                }

                _ = await db.SaveChangesAsync(ct);
                return existing ?? @event;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting event: {ProviderEventId}", @event.ProviderEventId);
                throw;
            }
        }

        /// <summary>Creates a new event in the database.</summary>
        public async Task<Event> CreateAsync(Event @event, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = await db.Events.AddAsync(@event, ct);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Event created: {EventId} ({ProviderEventId})", @event.Id, @event.ProviderEventId);
                return @event;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event: {ProviderEventId}", @event.ProviderEventId);
                throw;
            }
        }

        /// <summary>Updates an existing event's metadata (EventType, OccurredAtUtc, SnapshotUrl).</summary>
        public async Task UpdateAsync(Event @event, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Event? existing = await db.Events.FirstOrDefaultAsync(e => e.Id == @event.Id, ct);
                if (existing != null)
                {
                    existing.EventType = @event.EventType;
                    existing.OccurredAtUtc = @event.OccurredAtUtc;
                    existing.SnapshotUrl = @event.SnapshotUrl;
                    _ = db.Events.Update(existing);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Event updated: {EventId}", @event.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event: {EventId}", @event.Id);
                throw;
            }
        }

        /// <summary>Lists events for a device within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists events for all devices in a location within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);
        }

        /// <summary>Lists events by type for a device within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByDeviceEventTypeAndDateRangeAsync(
            Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId &&
                            e.EventType == eventType &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists events by type for all devices in a location within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByLocationEventTypeAndDateRangeAsync(
            Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.EventType == eventType &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct);
        }

        /// <summary>Gets event type summary (count by type) for a location within a date range.</summary>
        public async Task<Dictionary<string, int>> GetEventTypeSummaryAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Join(db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .GroupBy(x => x.Event.EventType)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        }

        /// <summary>Lists events that are unanswered or flagged for a device.</summary>
        public async Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .ToListAsync(ct);
        }

        /// <summary>Lists all events.</summary>
        public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Events.AsNoTracking().ToListAsync(ct);
        }

        /// <summary>Lists events with true database-level pagination (OrderBy+Skip+Take), unlike the full-table ListAsync.</summary>
        public async Task<PaginatedResult<Event>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            int totalCount = await db.Events.AsNoTracking().CountAsync(ct);

            var items = await db.Events.AsNoTracking()
                .OrderByDescending(e => e.OccurredAtUtc)
                .ThenBy(e => e.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginatedResult<Event>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>Updates an event's download failure status and timestamp.</summary>
        public async Task UpdateDownloadFailureAsync(Guid eventId, DateTime failureTime, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Event? @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                if (@event != null)
                {
                    @event.DownloadStatus = EventDownloadStatus.DownloadFailed;
                    @event.DownloadFailedAtUtc = failureTime;
                    _ = db.Events.Update(@event);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Event download failure recorded: {EventId} at {FailureTime}", eventId, failureTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event download failure: {EventId}", eventId);
                throw;
            }
        }

        /// <summary>Deletes an event.</summary>
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Event? @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                if (@event != null)
                {
                    _ = db.Events.Remove(@event);
                    _ = await db.SaveChangesAsync(ct);
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
