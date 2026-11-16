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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MediaStillRepository> _logger;

        public MediaStillRepository(IUnitOfWork unitOfWork, ILogger<MediaStillRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<MediaStillCapture> CreateAsync(MediaItem stillMediaItem, MediaStillCapture capture, CancellationToken ct)
        {
            if (stillMediaItem.Id != capture.MediaItemId)
            {
                throw new InvalidOperationException("capture.MediaItemId must match stillMediaItem.Id.");
            }

            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                // Both rows are added to the same DbContext instance and saved in one SaveChangesAsync
                // call, so they're written atomically (either both persist or neither does).
                _ = db.MediaItems.Add(stillMediaItem);
                _ = db.MediaStillCaptures.Add(capture);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);

            _logger.LogDebug(
                "Derived still {MediaItemId} captured from source {SourceMediaItemId} at offset {FrameOffsetMs}ms",
                capture.MediaItemId, capture.SourceMediaItemId, capture.FrameOffsetMs);

            return capture;
        }

        /// <inheritdoc />
        public async Task<MediaStillCapture?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().MediaStillCaptures.FirstOrDefaultAsync(c => c.MediaItemId == mediaItemId, ct), ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaStillCapture>> GetBySourceMediaItemIdAsync(Guid sourceMediaItemId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().MediaStillCaptures
                .Where(c => c.SourceMediaItemId == sourceMediaItemId)
                .ToListAsync(ct), ct);
        }
    }
}
