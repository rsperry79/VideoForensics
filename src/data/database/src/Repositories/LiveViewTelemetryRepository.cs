using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for live-view telemetry samples.</summary>
    public class LiveViewTelemetryRepository : ILiveViewTelemetryRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<LiveViewTelemetryRepository> _logger;

        /// <summary>Initializes a new instance of the LiveViewTelemetryRepository.</summary>
        public LiveViewTelemetryRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<LiveViewTelemetryRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Appends a new telemetry sample to the database.</summary>
        public async Task AddSampleAsync(LiveViewTelemetrySample sample, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                if (sample.Id == Guid.Empty)
                {
                    sample.Id = Guid.NewGuid();
                }

                _ = db.LiveViewTelemetrySamples.Add(sample);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Telemetry sample added: {SampleId}", sample.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding telemetry sample for session: {SessionId}", sample.SessionId);
                throw;
            }
        }

        /// <summary>Gets all telemetry samples for a live-view session.</summary>
        public async Task<IReadOnlyList<LiveViewTelemetrySample>> GetSamplesAsync(Guid sessionId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LiveViewTelemetrySamples
                .Where(s => s.SessionId == sessionId)
                .OrderBy(s => s.CapturedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Deletes telemetry samples older than the specified age.</summary>
        public async Task<int> PruneOlderThanAsync(TimeSpan age, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                DateTime cutoff = DateTime.UtcNow - age;
                int deletedCount = await db.LiveViewTelemetrySamples
                    .Where(s => s.CapturedAtUtc < cutoff)
                    .ExecuteDeleteAsync(ct);

                _logger.LogInformation("Pruned {DeletedCount} telemetry samples older than {Cutoff}", deletedCount, cutoff);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pruning telemetry samples");
                throw;
            }
        }
    }
}
