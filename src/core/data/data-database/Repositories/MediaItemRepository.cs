using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for MediaItem entities.</summary>
    public class MediaItemRepository : IMediaItemRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<MediaItemRepository> _logger;

        /// <summary>Initializes a new instance of the MediaItemRepository.</summary>
        public MediaItemRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<MediaItemRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a media item by ID.</summary>
        public async Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct);
        }

        /// <summary>Gets media items by device ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems.Where(m => m.DeviceId == deviceId).ToListAsync(ct);
        }

        /// <summary>Gets media items by device ID within a date range.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets a media item by SHA-256 hash.</summary>
        public async Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems.FirstOrDefaultAsync(m => m.Sha256Hash == sha256Hash, ct);
        }

        /// <summary>Gets media items by download event ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems.Where(m => m.DownloadEventId == downloadEventId).ToListAsync(ct);
        }

        /// <summary>Lists all media items.</summary>
        public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaItems.ToListAsync(ct);
        }

        /// <summary>Adds a new media item.</summary>
        public async Task AddAsync(MediaItem mediaItem, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.MediaItems.Add(mediaItem);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Media item added: {MediaItemId} ({FileName})", mediaItem.Id, mediaItem.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding media item: {FileName}", mediaItem.FileName);
                throw;
            }
        }

        /// <summary>Updates an existing media item.</summary>
        public async Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.MediaItems.Update(mediaItem);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Media item updated: {MediaItemId}", mediaItem.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating media item: {MediaItemId}", mediaItem.Id);
                throw;
            }
        }

        /// <summary>Deletes a media item.</summary>
        public async Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var mediaItem = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct);
                if (mediaItem != null)
                {
                    db.MediaItems.Remove(mediaItem);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Media item deleted: {MediaItemId}", mediaItemId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media item: {MediaItemId}", mediaItemId);
                throw;
            }
        }
    }
}
