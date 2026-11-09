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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EventRepository> _logger;

        /// <summary>Initializes a new instance of the EventRepository.</summary>
        public EventRepository(IUnitOfWork unitOfWork, ILogger<EventRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets an event by ID.</summary>
        public async Task<Event?> GetAsync(Guid eventId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct), ct);
        }

        /// <summary>Gets an event by device ID and provider event ID.</summary>
        public async Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking().FirstOrDefaultAsync(
                e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId, ct), ct);
        }

        /// <summary>Gets an event by API source hash for deduplication.</summary>
        public async Task<Event?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking().FirstOrDefaultAsync(e => e.ApiSourceHash == apiSourceHash, ct), ct);
        }

        /// <summary>Upserts (inserts or updates) an event by device ID and provider event ID.</summary>
        public async Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    Event? existing = await db.Events.FirstOrDefaultAsync(
                        e => e.DeviceId == @event.DeviceId && e.ProviderEventId == @event.ProviderEventId, ct);

                    if (existing == null)
                    {
                        _ = db.Events.Add(@event);
                        _logger.LogDebug("Event inserted: {EventId} ({ProviderEventId})", @event.Id, SanitizeForLog(@event.ProviderEventId));
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

                        // Only the rekey on a match clears the queue flag; a re-upsert of an awaiting row keeps it.
                        existing.AwaitingDingId |= @event.AwaitingDingId;

                        // Update DownloadStatus based on DownloadedAtUtc
                        if (existing.DownloadedAtUtc.HasValue)
                        {
                            existing.DownloadStatus = EventDownloadStatus.Downloaded;
                        }

                        _ = db.Events.Update(existing);
                        _logger.LogDebug("Event upserted (updated): {EventId}", @event.Id);
                    }

                    _ = await db.SaveChangesAsync(ct);
                    return existing ?? @event;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error upserting event: {ProviderEventId}: {Message} ({ErrorType})", SanitizeForLog(@event.ProviderEventId), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Creates a new event in the database.</summary>
        public async Task<Event> CreateAsync(Event @event, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = await db.Events.AddAsync(@event, ct);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogDebug("Event created: {EventId} ({ProviderEventId})", @event.Id, SanitizeForLog(@event.ProviderEventId));
                    return @event;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating event: {ProviderEventId}: {Message} ({ErrorType})", SanitizeForLog(@event.ProviderEventId), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing event's metadata (EventType, OccurredAtUtc, SnapshotUrl).</summary>
        public async Task UpdateAsync(Event @event, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    Event? existing = await db.Events.FirstOrDefaultAsync(e => e.Id == @event.Id, ct);
                    if (existing != null)
                    {
                        existing.EventType = @event.EventType;
                        existing.OccurredAtUtc = @event.OccurredAtUtc;
                        existing.SnapshotUrl = @event.SnapshotUrl;
                        _ = db.Events.Update(existing);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Event updated: {EventId}", @event.Id);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating event: {EventId}: {Message} ({ErrorType})", @event.Id, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists events for a device within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListAwaitingDingIdAsync(Guid deviceId, int maxAttempts, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.AwaitingDingId && e.DownloadAttempts < maxAttempts && e.DownloadStatus != EventDownloadStatus.Downloaded)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <inheritdoc />
        public async Task RecordRetryPassAsync(Guid eventId, int maxAttempts, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Event? evt = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                if (evt is null)
                {
                    return null;
                }

                evt.DownloadAttempts++;
                if (evt.DownloadAttempts >= maxAttempts)
                {
                    evt.DownloadStatus = EventDownloadStatus.Unavailable;
                    evt.AwaitingDingId = false;
                }

                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        /// <summary>Gets the earliest OccurredAtUtc among a device's events, or null when it has none.</summary>
        public async Task<DateTime?> GetEarliestOccurredAtUtcAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .MinAsync(e => (DateTime?)e.OccurredAtUtc, ct), ct);
        }

        /// <summary>Keyset-paged read of a device's events ordered by OccurredAtUtc then Id.</summary>
        public async Task<IReadOnlyList<Event>> ListByDevicePageAsync(
            Guid deviceId, DateTime? afterOccurredAtUtc, Guid? afterId, int take, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                IQueryable<Event> query = context.Db().Events.AsNoTracking().Where(e => e.DeviceId == deviceId);
                if (afterOccurredAtUtc.HasValue)
                {
                    DateTime at = afterOccurredAtUtc.Value;
                    Guid id = afterId ?? Guid.Empty;
                    query = query.Where(e => e.OccurredAtUtc > at || (e.OccurredAtUtc == at && e.Id.CompareTo(id) > 0));
                }

                return await query.OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).Take(take).ToListAsync(ct);
            }, ct);
        }

        /// <summary>Lists events for all devices in a location within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Join(context.Db().Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists events by type for a device within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByDeviceEventTypeAndDateRangeAsync(
            Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId &&
                            e.EventType == eventType &&
                            e.OccurredAtUtc >= fromUtc &&
                            e.OccurredAtUtc <= toUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists events by type for all devices in a location within a date range.</summary>
        public async Task<IReadOnlyList<Event>> ListByLocationEventTypeAndDateRangeAsync(
            Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Join(context.Db().Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.EventType == eventType &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToListAsync(ct), ct);
        }

        /// <summary>Gets event type summary (count by type) for a location within a date range.</summary>
        public async Task<Dictionary<string, int>> GetEventTypeSummaryAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Join(context.Db().Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId &&
                            x.Event.OccurredAtUtc >= fromUtc &&
                            x.Event.OccurredAtUtc <= toUtc)
                .GroupBy(x => x.Event.EventType)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct), ct);
        }

        /// <summary>Lists events that are unanswered or flagged for a device.</summary>
        public async Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists all events.</summary>
        public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Events.AsNoTracking().ToListAsync(ct), ct);
        }

        /// <summary>Lists events with true database-level pagination (OrderBy+Skip+Take), unlike the full-table ListAsync.</summary>
        public async Task<PaginatedResult<Event>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<PaginatedResult<Event>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                int totalCount = await db.Events.AsNoTracking().CountAsync(ct);

                List<Event> items = await db.Events.AsNoTracking()
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
            }, ct);
        }

        /// <summary>
        /// Pages events from any of the given devices within a UTC range, newest first. Filtering, sorting, paging and counting
        /// all run in SQL. The id list is materialized once so EF translates a plain IN list.
        /// </summary>
        public async Task<EventPage> ListPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? excludeEventTypes,
            IReadOnlyCollection<string>? includeEventTypes, int skip, int take, CancellationToken ct)
        {
            // Bounds are checked here as well as at the endpoint: an unbounded take would turn the page back into a full scan.
            ArgumentOutOfRangeException.ThrowIfLessThan(skip, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 500);
            ArgumentOutOfRangeException.ThrowIfLessThan(deviceIds.Count, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(deviceIds.Count, 64);

            Guid[] ids = deviceIds.ToArray();
            string[] excluded = excludeEventTypes?.ToArray() ?? [];
            string[] included = includeEventTypes?.ToArray() ?? [];

            return await _unitOfWork.ReadAsync<EventPage>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                IQueryable<Event> matches = db.Events
                    .AsNoTracking()
                    .Where(e => ids.Contains(e.DeviceId) && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc && !excluded.Contains(e.EventType)
                        && (included.Length == 0
                            || (included.Contains(e.EventType) && !(e.EventType == "motion" && db.EventDetections.Any(d => d.EventId == e.Id && d.DetectionType != null && d.DetectionType != "motion" && d.DetectionType != "other_motion")))
                            || db.EventDetections.Any(d => d.EventId == e.Id && d.DetectionType != null && included.Contains(d.DetectionType))));

                int total = await matches.CountAsync(ct);
                List<Event> items = await matches
                    .OrderByDescending(e => e.OccurredAtUtc)
                    .ThenBy(e => e.Id)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(ct);

                return new EventPage(items, total);
            }, ct);
        }

        /// <summary>Updates an event's download failure status and timestamp.</summary>
        public async Task UpdateDownloadFailureAsync(Guid eventId, DateTime failureTime, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    Event? @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                    if (@event != null)
                    {
                        @event.DownloadStatus = EventDownloadStatus.DownloadFailed;
                        @event.DownloadFailedAtUtc = failureTime;
                        _ = db.Events.Update(@event);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Event download failure recorded: {EventId} at {FailureTime}", eventId, failureTime);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating event download failure: {EventId}: {Message} ({ErrorType})", eventId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes an event.</summary>
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    Event? @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                    if (@event != null)
                    {
                        _ = db.Events.Remove(@event);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Event deleted: {EventId}", eventId);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting event: {EventId}: {Message} ({ErrorType})", eventId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
