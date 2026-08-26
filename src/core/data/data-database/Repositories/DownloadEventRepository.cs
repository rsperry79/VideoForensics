using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for DownloadEvent entities.</summary>
    public class DownloadEventRepository : IDownloadEventRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DownloadEventRepository> _logger;

        /// <summary>Initializes a new instance of the DownloadEventRepository.</summary>
        public DownloadEventRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DownloadEventRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a download event by ID.</summary>
        public async Task<DownloadEvent?> GetAsync(Guid eventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadEvents.FirstOrDefaultAsync(de => de.Id == eventId, ct);
        }

        /// <summary>Gets download events by device ID.</summary>
        public async Task<IReadOnlyList<DownloadEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadEvents.Where(de => de.DeviceId == deviceId).ToListAsync(ct);
        }

        /// <summary>Gets the latest successful download event time for a device (watermark).</summary>
        public async Task<DateTime?> GetLatestSuccessfulEventTimeAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var latest = await db.DownloadEvents
                .Where(de => de.DeviceId == deviceId && de.Success && de.DownloadCompletedUtc.HasValue)
                .OrderByDescending(de => de.EventOccurredAtUtc)
                .FirstOrDefaultAsync(ct);
            return latest?.EventOccurredAtUtc;
        }

        /// <summary>Checks if a download event exists for a given provider event ID on a device.</summary>
        public async Task<bool> ExistsForProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadEvents.AnyAsync(
                de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId, ct);
        }

        /// <summary>Gets a download event by device ID and provider event ID.</summary>
        public async Task<DownloadEvent?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadEvents.FirstOrDefaultAsync(
                de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId, ct);
        }

        /// <summary>Lists all download events.</summary>
        public async Task<IReadOnlyList<DownloadEvent>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadEvents.ToListAsync(ct);
        }

        /// <summary>Adds a new download event.</summary>
        public async Task AddAsync(DownloadEvent downloadEvent, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DownloadEvents.Add(downloadEvent);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Download event added: {DownloadEventId} ({ProviderEventId})",
                    downloadEvent.Id, downloadEvent.ProviderEventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding download event: {ProviderEventId}", downloadEvent.ProviderEventId);
                throw;
            }
        }

        /// <summary>Updates an existing download event.</summary>
        public async Task UpdateAsync(DownloadEvent downloadEvent, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DownloadEvents.Update(downloadEvent);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Download event updated: {DownloadEventId}", downloadEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating download event: {DownloadEventId}", downloadEvent.Id);
                throw;
            }
        }

        /// <summary>Deletes a download event.</summary>
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var downloadEvent = await db.DownloadEvents.FirstOrDefaultAsync(de => de.Id == eventId, ct);
                if (downloadEvent != null)
                {
                    db.DownloadEvents.Remove(downloadEvent);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Download event deleted: {DownloadEventId}", eventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting download event: {DownloadEventId}", eventId);
                throw;
            }
        }
    }
}
