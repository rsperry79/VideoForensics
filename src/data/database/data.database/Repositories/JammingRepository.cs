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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<JammingRepository> _logger;

        /// <summary>Initializes a new instance of the JammingRepository.</summary>
        public JammingRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<JammingRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Upserts (inserts or updates) a jamming incident record.</summary>
        public async Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
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

                    db.JammingIncidentRecords.Add(incident);
                    _logger.LogInformation("Jamming incident inserted: {IncidentId}", incident.Id);
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
                    db.JammingIncidentRecords.Update(existing);
                    _logger.LogInformation("Jamming incident upserted (updated): {IncidentId}", incident.Id);
                }

                await db.SaveChangesAsync(ct);
                return existing ?? incident;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting jamming incident: {IncidentId}", incident.Id);
                throw;
            }
        }

        /// <summary>Lists jamming incidents, optionally filtered by device and date range.</summary>
        public async Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(
            Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var query = db.JammingIncidentRecords.AsQueryable();

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
        }

        /// <summary>Gets the jamming stats summary for a device.</summary>
        public async Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.JammingStatsSummaries.FirstOrDefaultAsync(j => j.DeviceId == deviceId, ct);
        }

        /// <summary>Lists jamming stats summaries for all devices.</summary>
        public async Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.JammingStatsSummaries.ToListAsync(ct);
        }

        /// <summary>Recomputes and upserts the JammingStatsSummary row for a device from its current JammingIncidentRecord rows.</summary>
        public async Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var incidents = await db.JammingIncidentRecords
                    .Where(j => j.DeviceId == deviceId)
                    .ToListAsync(ct);

                var existing = await db.JammingStatsSummaries.FirstOrDefaultAsync(j => j.DeviceId == deviceId, ct);
                var summary = existing ?? new JammingStatsSummary { Id = Guid.NewGuid(), DeviceId = deviceId };

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

                if (existing == null)
                {
                    db.JammingStatsSummaries.Add(summary);
                }
                else
                {
                    db.JammingStatsSummaries.Update(summary);
                }

                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Recomputed jamming stats for device {DeviceId}: {IncidentCount} incidents", deviceId, summary.IncidentCount);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recomputing jamming stats for device: {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
