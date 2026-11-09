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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DownloadEventRepository> _logger;

        /// <summary>Initializes a new instance of the DownloadEventRepository.</summary>
        public DownloadEventRepository(IUnitOfWork unitOfWork, ILogger<DownloadEventRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a download event by ID.</summary>
        public async Task<DownloadEvent?> GetAsync(Guid eventId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DownloadEvents.FirstOrDefaultAsync(de => de.Id == eventId, ct), ct);
        }

        /// <summary>Gets download events by device ID.</summary>
        public async Task<IReadOnlyList<DownloadEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DownloadEvents.Where(de => de.DeviceId == deviceId).ToListAsync(ct), ct);
        }

        /// <summary>Gets the latest successful download event time for a device (watermark).</summary>
        public async Task<DateTime?> GetLatestSuccessfulEventTimeAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                DownloadEvent? latest = await context.Db().DownloadEvents
                    .Where(de => de.DeviceId == deviceId && de.Success && de.DownloadCompletedUtc.HasValue)
                    .OrderByDescending(de => de.EventOccurredAtUtc)
                    .FirstOrDefaultAsync(ct);
                return latest?.EventOccurredAtUtc;
            }, ct);
        }

        /// <summary>Checks if a download event exists for a given provider event ID on a device.</summary>
        public async Task<bool> ExistsForProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DownloadEvents.AnyAsync(
                de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId, ct), ct);
        }

        /// <summary>Gets a download event by device ID and provider event ID.</summary>
        public async Task<DownloadEvent?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DownloadEvents.FirstOrDefaultAsync(
                de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId, ct), ct);
        }

        /// <summary>Lists all download events.</summary>
        public async Task<IReadOnlyList<DownloadEvent>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DownloadEvents.ToListAsync(ct), ct);
        }

        /// <summary>Adds a new download event.</summary>
        public async Task AddAsync(DownloadEvent downloadEvent, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.DownloadEvents.Add(downloadEvent);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Download event added: {DownloadEventId} ({ProviderEventId})",
                    downloadEvent.Id, downloadEvent.ProviderEventId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding download event: {ProviderEventId}: {Message} ({ErrorType})", downloadEvent.ProviderEventId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing download event.</summary>
        public async Task UpdateAsync(DownloadEvent downloadEvent, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.DownloadEvents.Update(downloadEvent);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Download event updated: {DownloadEventId}", downloadEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating download event: {DownloadEventId}: {Message} ({ErrorType})", downloadEvent.Id, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes a download event.</summary>
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            try
            {
                bool deleted = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    DownloadEvent? downloadEvent = await db.DownloadEvents.FirstOrDefaultAsync(de => de.Id == eventId, ct);
                    if (downloadEvent == null)
                    {
                        return false;
                    }

                    _ = db.DownloadEvents.Remove(downloadEvent);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted)
                {
                    _logger.LogDebug("Download event deleted: {DownloadEventId}", eventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting download event: {DownloadEventId}: {Message} ({ErrorType})", eventId, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
