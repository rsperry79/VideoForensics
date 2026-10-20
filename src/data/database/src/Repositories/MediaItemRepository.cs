using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for MediaItem entities.</summary>
    public class MediaItemRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<MediaItemRepository> logger) : IMediaItemRepository
    {
        /// <summary>Gets a media item by ID.</summary>
        public async Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct);
        }

        /// <summary>Gets media items by device ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.Where(m => m.DeviceId == deviceId).ToListAsync(ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems
                .AsNoTracking()
                .Where(m => m.DeviceId == deviceId)
                .OrderByDescending(m => m.RecordedAtUtc)
                .Take(limit)
                .ToListAsync(ct);
        }

        /// <summary>Gets media items by device ID within a date range.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets a media item by SHA-256 hash.</summary>
        public async Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.FirstOrDefaultAsync(m => m.Sha256Hash == sha256Hash, ct);
        }

        /// <summary>Gets a media item by API source hash for deduplication.</summary>
        public async Task<MediaItem?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.FirstOrDefaultAsync(m => m.ApiSourceHash == apiSourceHash, ct);
        }

        /// <summary>Gets media items by download event ID.</summary>
        public async Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.Where(m => m.DownloadEventId == downloadEventId).ToListAsync(ct);
        }

        /// <summary>Lists all media items.</summary>
        public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems.ToListAsync(ct);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The video test mirrors the thumbnail service's rule (format name contains "video" or "mp4"), so the
        /// database returns only rows that the backfill would act on.
        /// </remarks>
        public async Task<IReadOnlyList<MediaItem>> ListVideosWithoutThumbnailAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            return await db.MediaItems
                .AsNoTracking()
                .Where(m => !m.IsPurged
                    && m.ThumbnailPath == null
                    && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .ToListAsync(ct);
        }

        /// <summary>Adds a new media item.</summary>
        public async Task AddAsync(MediaItem mediaItem, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.MediaItems.Add(mediaItem);
                _ = await db.SaveChangesAsync(ct);
                // Guard: the sanitized name is only worth computing when debug logging is on.
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Media item added: {FileName}", SanitizeForLog(mediaItem.FileName));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding media item: {FileName}", SanitizeForLog(mediaItem.FileName));
                throw;
            }
        }

        /// <summary>Updates an existing media item.</summary>
        public async Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.MediaItems.Update(mediaItem);
                _ = await db.SaveChangesAsync(ct);
                // Guard: the sanitized name is only worth computing when debug logging is on.
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Media item updated: {FileName}", SanitizeForLog(mediaItem.FileName));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating media item: {FileName}", SanitizeForLog(mediaItem.FileName));
                throw;
            }
        }

        /// <summary>Deletes a media item.</summary>
        public async Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
            try
            {
                MediaItem? mediaItem = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId, ct);
                if (mediaItem != null)
                {
                    _ = db.MediaItems.Remove(mediaItem);
                    _ = await db.SaveChangesAsync(ct);
                    // Guard: keeps the debug log call off the hot path when debug logging is off.
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Media item deleted: {MediaItemId}", mediaItemId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting media item: {MediaItemId}", mediaItemId);
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
