using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for derived still media items and their capture provenance.</summary>
    public class MediaStillRepository : IMediaStillRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<MediaStillRepository> _logger;

        public MediaStillRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<MediaStillRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<MediaStillCapture> CreateAsync(MediaItem stillMediaItem, MediaStillCapture capture, CancellationToken ct)
        {
            if (stillMediaItem.Id != capture.MediaItemId)
            {
                throw new InvalidOperationException("capture.MediaItemId must match stillMediaItem.Id.");
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            // Both rows are added to the same DbContext instance and saved in one SaveChangesAsync
            // call, so they're written atomically (either both persist or neither does).
            _ = db.MediaItems.Add(stillMediaItem);
            _ = db.MediaStillCaptures.Add(capture);
            _ = await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Derived still {MediaItemId} captured from source {SourceMediaItemId} at offset {FrameOffsetMs}ms by {CapturedBy}",
                capture.MediaItemId, capture.SourceMediaItemId, capture.FrameOffsetMs, capture.CapturedByOperator);

            return capture;
        }

        /// <inheritdoc />
        public async Task<MediaStillCapture?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaStillCaptures.FirstOrDefaultAsync(c => c.MediaItemId == mediaItemId, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaStillCapture>> GetBySourceMediaItemIdAsync(Guid sourceMediaItemId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaStillCaptures
                .Where(c => c.SourceMediaItemId == sourceMediaItemId)
                .ToListAsync(ct);
        }
    }
}
