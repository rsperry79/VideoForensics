using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for jamming incident records and stats summaries.</summary>
    public class JammingRepository : IJammingRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JammingRepository> _logger;
        private readonly ICaseRepository? _caseRepository;

        /// <summary>Initializes a new instance of the JammingRepository.</summary>
        public JammingRepository(IUnitOfWork unitOfWork, ILogger<JammingRepository> logger, ICaseRepository? caseRepository = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _caseRepository = caseRepository;
        }

        /// <summary>Upserts (inserts or updates) a jamming incident record.</summary>
        public async Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct)
        {
            try
            {
                (JammingIncidentRecord saved, bool isInsert) = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    JammingIncidentRecord? existing = null;
                    if (incident.Id != Guid.Empty)
                    {
                        existing = await db.JammingIncidentRecords.FirstOrDefaultAsync(j => j.Id == incident.Id, ct);
                    }

                    if (existing == null)
                    {
                        if (incident.Id == Guid.Empty)
                        {
                            incident.Id = Guid.NewGuid();
                        }

                        _ = db.JammingIncidentRecords.Add(incident);
                        _logger.LogDebug("Jamming incident inserted: {IncidentId}", incident.Id);
                    }
                    else
                    {
                        existing.DeviceId = incident.DeviceId;
                        existing.StartUtc = incident.StartUtc;
                        existing.EndUtc = incident.EndUtc;
                        existing.AffectedEventCount = incident.AffectedEventCount;
                        existing.AverageDegradationDb = incident.AverageDegradationDb;
                        existing.Confidence = incident.Confidence;
                        existing.DetectedAtUtc = incident.DetectedAtUtc;
                        existing.Notes = incident.Notes;
                        existing.Source = incident.Source;
                        // Preserve CaseId if incoming is null, otherwise update it
                        if (incident.CaseId.HasValue)
                        {
                            existing.CaseId = incident.CaseId;
                        }

                        _ = db.JammingIncidentRecords.Update(existing);
                        _logger.LogDebug("Jamming incident upserted (updated): {IncidentId}", incident.Id);
                    }

                    bool insert = existing == null;

                    _ = await db.SaveChangesAsync(ct);
                    return (existing ?? incident, insert);
                }, ct);

                // M3: Auto-create case and alert on new jamming incident INSERT. Runs after the write commits, as before.
                if (isInsert && _caseRepository is not null)
                {
                    try
                    {
                        _ = await _caseRepository.CreateFromJammingDetectionAsync(incident, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to create case for jamming incident {IncidentId}; incident saved but case not linked: {Message} ({ErrorType})", incident.Id, ex.Message, ex.GetType().Name);
                    }
                }

                return saved;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error upserting jamming incident: {IncidentId}: {Message} ({ErrorType})", incident.Id, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists jamming incidents, optionally filtered by device and date range.</summary>
        public async Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(
            Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                IQueryable<JammingIncidentRecord> query = context.Db().JammingIncidentRecords.AsQueryable();

                if (deviceId.HasValue)
                {
                    query = query.Where(j => j.DeviceId == deviceId.Value);
                }

                if (fromUtc.HasValue)
                {
                    query = query.Where(j => j.StartUtc >= fromUtc.Value);
                }

                if (toUtc.HasValue)
                {
                    query = query.Where(j => j.StartUtc <= toUtc.Value);
                }

                return await query.ToListAsync(ct);
            }, ct);
        }

        /// <summary>Gets the jamming stats summary for a device.</summary>
        public Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().JammingStatsSummaries.FirstOrDefaultAsync(j => j.DeviceId == deviceId, ct), ct);
        }

        /// <summary>Lists jamming stats summaries for all devices.</summary>
        public async Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().JammingStatsSummaries.ToListAsync(ct), ct);
        }

        /// <summary>Recomputes and upserts the JammingStatsSummary row for a device from its current JammingIncidentRecord rows.</summary>
        public async Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    List<JammingIncidentRecord> incidents = await db.JammingIncidentRecords
                        .Where(j => j.DeviceId == deviceId)
                        .ToListAsync(ct);

                    JammingStatsSummary? existing = await db.JammingStatsSummaries.FirstOrDefaultAsync(j => j.DeviceId == deviceId, ct);
                    JammingStatsSummary summary = existing ?? new JammingStatsSummary { Id = Guid.NewGuid(), DeviceId = deviceId };

                    summary.IncidentCount = incidents.Count;
                    summary.TotalJammedDurationMinutes = incidents.Sum(i => (i.EndUtc - i.StartUtc).TotalMinutes);
                    summary.AverageDegradationDb = incidents.Count > 0 ? incidents.Average(i => i.AverageDegradationDb) : 0;
                    summary.MaxDegradationDb = incidents.Count > 0 ? incidents.Max(i => i.AverageDegradationDb) : 0;
                    summary.LowConfidenceCount = incidents.Count(i => i.Confidence == JammingConfidenceLevel.Low);
                    summary.MediumConfidenceCount = incidents.Count(i => i.Confidence == JammingConfidenceLevel.Medium);
                    summary.HighConfidenceCount = incidents.Count(i => i.Confidence == JammingConfidenceLevel.High);
                    summary.DefiniteConfidenceCount = incidents.Count(i => i.Confidence == JammingConfidenceLevel.Definite);
                    summary.FirstIncidentUtc = incidents.Count > 0 ? incidents.Min(i => i.StartUtc) : null;
                    summary.LastIncidentUtc = incidents.Count > 0 ? incidents.Max(i => i.StartUtc) : null;
                    summary.LastUpdatedUtc = DateTime.UtcNow;

                    _ = existing == null ? db.JammingStatsSummaries.Add(summary) : db.JammingStatsSummaries.Update(summary);

                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogDebug("Recomputed jamming stats for device {DeviceId}: {IncidentCount} incidents", deviceId, summary.IncidentCount);
                    return summary;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recomputing jamming stats for device: {DeviceId}: {Message} ({ErrorType})", deviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets a jamming incident record by ID.</summary>
        public Task<JammingIncidentRecord?> GetIncidentAsync(Guid incidentId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().JammingIncidentRecords.FirstOrDefaultAsync(j => j.Id == incidentId, ct), ct);
        }
    }
}
