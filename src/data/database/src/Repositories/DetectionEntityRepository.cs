using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Detection-related entity writes. Each write runs through the unit of work: it joins an open unit of work, or commits on its own.
    /// </summary>
    public class DetectionEntityRepository : IDetectionEntityProvider
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DetectionEntityRepository> _logger;

        /// <summary>Initializes a new instance of the DetectionEntityRepository.</summary>
        public DetectionEntityRepository(IUnitOfWork unitOfWork, ILogger<DetectionEntityRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task AddMediaItemDetectionAsync(MediaItemDetection detection, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (detection != null)
                {
                    _ = context.Db().MediaItemDetections.Add(detection);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddDetectedPersonsAsync(List<DetectedPerson> persons, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (persons != null && persons.Count > 0)
                {
                    context.Db().DetectedPersons.AddRange(persons);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddDetectionTypeOccurrencesAsync(List<DetectionTypeOccurrence> occurrences, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (occurrences != null && occurrences.Count > 0)
                {
                    context.Db().DetectionTypeOccurrences.AddRange(occurrences);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddEventDetectionAsync(EventDetection detection, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (detection != null)
                {
                    _ = context.Db().EventDetections.Add(detection);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public Task<Guid> UpsertEventDetectionAsync(EventDetection detection, CancellationToken ct)
        {
            return _unitOfWork.ExecuteAsync<Guid>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                // One detection row per event: key on EventId. Check unsaved rows first so two upserts in
                // the same transaction don't both insert.
                EventDetection? existing = db.EventDetections.Local.FirstOrDefault(d => d.EventId == detection.EventId)
                    ?? await db.EventDetections.FirstOrDefaultAsync(d => d.EventId == detection.EventId, ct);

                if (existing == null)
                {
                    _ = db.EventDetections.Add(detection);
                    return detection.Id;
                }

                if (!ReferenceEquals(existing, detection))
                {
                    existing.PersonDetected = detection.PersonDetected;
                    existing.StreamBroken = detection.StreamBroken;
                    existing.DetectionType = detection.DetectionType;
                    existing.FullDescription = detection.FullDescription;
                    existing.ShortDescription = detection.ShortDescription;
                    existing.Similarity = detection.Similarity;
                    existing.Anomaly = detection.Anomaly;
                    existing.Confidence = detection.Confidence;
                    existing.ModelVersion = detection.ModelVersion;
                }

                // Children are re-added by the caller from the latest data, so drop the previous set.
                Guid detectionId = existing.Id;
                db.EventDetectionZones.RemoveRange(await db.EventDetectionZones.Where(z => z.EventDetectionId == detectionId).ToListAsync(ct));
                db.EventDetectionTypeOccurrences.RemoveRange(await db.EventDetectionTypeOccurrences.Where(o => o.EventDetectionId == detectionId).ToListAsync(ct));

                detection.Id = detectionId;
                return detectionId;
            }, ct);
        }

        public async Task ClearEventAlertsAndPersonsAsync(Guid eventId, bool clearAlerts, bool clearPersons, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                if (clearAlerts)
                {
                    db.EventSecurityAlerts.RemoveRange(await db.EventSecurityAlerts.Where(a => a.EventId == eventId).ToListAsync(ct));
                }

                if (clearPersons)
                {
                    db.EventDetectedPersons.RemoveRange(await db.EventDetectedPersons.Where(p => p.EventId == eventId).ToListAsync(ct));
                }

                return null;
            }, ct);
        }

        public async Task AddEventDetectionZonesAsync(List<EventDetectionZone> zones, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (zones != null && zones.Count > 0)
                {
                    context.Db().EventDetectionZones.AddRange(zones);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddEventSecurityAlertsAsync(List<EventSecurityAlert> alerts, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (alerts != null && alerts.Count > 0)
                {
                    context.Db().EventSecurityAlerts.AddRange(alerts);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddEventDetectedPersonsAsync(List<EventDetectedPerson> persons, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (persons != null && persons.Count > 0)
                {
                    context.Db().EventDetectedPersons.AddRange(persons);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }

        public async Task AddEventDetectionTypeOccurrencesAsync(List<EventDetectionTypeOccurrence> occurrences, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(context =>
            {
                if (occurrences != null && occurrences.Count > 0)
                {
                    context.Db().EventDetectionTypeOccurrences.AddRange(occurrences);
                }

                return Task.FromResult<object?>(null);
            }, ct);
        }
    }
}
