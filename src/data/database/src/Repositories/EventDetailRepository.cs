using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Reads the detection child rows for a batch of events. Each table is read with one <c>Contains</c> query, so the
    /// number of round trips is fixed (five) regardless of how many events are requested.
    /// </summary>
    public class EventDetailRepository : IEventDetailRepository
    {
        private const int MaxEventIds = 200;

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<EventDetailRepository> _logger;

        /// <summary>Initializes a new instance of the EventDetailRepository.</summary>
        public EventDetailRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<EventDetailRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Returns one <see cref="EventDetail"/> per requested event that has any detail rows, in the order the ids were requested.
        /// </summary>
        public async Task<IReadOnlyList<EventDetail>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
        {
            // Bound the IN lists here as well as at the endpoint so an unbounded caller cannot turn this into a scan.
            ArgumentOutOfRangeException.ThrowIfGreaterThan(eventIds.Count, MaxEventIds);
            if (eventIds.Count == 0)
            {
                return [];
            }

            Guid[] ids = eventIds.Distinct().ToArray();

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            List<EventDetection> detections = await db.EventDetections.AsNoTracking()
                .Where(d => ids.Contains(d.EventId))
                .ToListAsync(ct);
            Guid[] detectionIds = detections.Select(d => d.Id).ToArray();

            List<EventDetectionTypeOccurrence> occurrences = await db.EventDetectionTypeOccurrences.AsNoTracking()
                .Where(o => detectionIds.Contains(o.EventDetectionId))
                .OrderBy(o => o.DetectedAtUtc)
                .ToListAsync(ct);

            List<EventDetectionZone> zones = await db.EventDetectionZones.AsNoTracking()
                .Where(z => detectionIds.Contains(z.EventDetectionId))
                .OrderBy(z => z.ZoneId)
                .ToListAsync(ct);

            // People and alerts hang off the event itself, not the detection row.
            List<EventDetectedPerson> people = await db.EventDetectedPersons.AsNoTracking()
                .Where(p => ids.Contains(p.EventId))
                .OrderBy(p => p.ProfileId)
                .ToListAsync(ct);

            List<EventSecurityAlert> alerts = await db.EventSecurityAlerts.AsNoTracking()
                .Where(a => ids.Contains(a.EventId))
                .OrderBy(a => a.Id)
                .ToListAsync(ct);

            // Group the child rows in memory by their parent key. This is a join over rows already filtered in SQL, not paging.
            Dictionary<Guid, EventDetection> detectionByEvent = detections
                .GroupBy(d => d.EventId)
                .ToDictionary(g => g.Key, g => g.First());
            Dictionary<Guid, Guid> eventByDetection = detections.ToDictionary(d => d.Id, d => d.EventId);
            Dictionary<Guid, List<EventDetectionTypeOccurrence>> occurrencesByEvent = GroupByEvent(occurrences, o => eventByDetection[o.EventDetectionId]);
            Dictionary<Guid, List<EventDetectionZone>> zonesByEvent = GroupByEvent(zones, z => eventByDetection[z.EventDetectionId]);
            Dictionary<Guid, List<EventDetectedPerson>> peopleByEvent = GroupByEvent(people, p => p.EventId);
            Dictionary<Guid, List<EventSecurityAlert>> alertsByEvent = GroupByEvent(alerts, a => a.EventId);

            var result = new List<EventDetail>();
            foreach (Guid eventId in ids)
            {
                bool hasRows = detectionByEvent.ContainsKey(eventId)
                    || peopleByEvent.ContainsKey(eventId)
                    || alertsByEvent.ContainsKey(eventId);
                if (!hasRows)
                {
                    continue;
                }

                result.Add(new EventDetail(
                    eventId,
                    detectionByEvent.GetValueOrDefault(eventId),
                    occurrencesByEvent.GetValueOrDefault(eventId) ?? [],
                    zonesByEvent.GetValueOrDefault(eventId) ?? [],
                    peopleByEvent.GetValueOrDefault(eventId) ?? [],
                    alertsByEvent.GetValueOrDefault(eventId) ?? []));
            }

            _logger.LogDebug("Loaded event detail rows for {EventCount} of {RequestedCount} events", result.Count, ids.Length);
            return result;
        }

        /// <summary>Groups rows by the event each one belongs to, keeping the incoming order within each group.</summary>
        private static Dictionary<Guid, List<T>> GroupByEvent<T>(IEnumerable<T> rows, Func<T, Guid> eventKey)
        {
            return rows.GroupBy(eventKey).ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
