using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for derived "still" media items and their capture provenance.</summary>
    public interface IMediaStillRepository
    {
        /// <summary>
        /// Atomically persists a derived still's <see cref="MediaItem"/> row and its
        /// <see cref="MediaStillCapture"/> provenance row (single transaction: either both are
        /// written or neither is).
        /// </summary>
        /// <param name="stillMediaItem">The new MediaItem row representing the still's file.</param>
        /// <param name="capture">The capture provenance row; its MediaItemId must equal stillMediaItem.Id.</param>
        Task<MediaStillCapture> CreateAsync(MediaItem stillMediaItem, MediaStillCapture capture, CancellationToken ct);

        /// <summary>Gets the capture provenance for a derived still by its MediaItem ID, or null if not a still.</summary>
        Task<MediaStillCapture?> GetAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets every still derived from the given source media item.</summary>
        Task<IReadOnlyList<MediaStillCapture>> GetBySourceMediaItemIdAsync(Guid sourceMediaItemId, CancellationToken ct);
    }
}
