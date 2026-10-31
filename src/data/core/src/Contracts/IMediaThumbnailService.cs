using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Makes sure a video has a stored still thumbnail, so evidence views never have to decode video to show one.</summary>
    public interface IMediaThumbnailService
    {
        /// <summary>
        /// Ensures a thumbnail sidecar exists for a video and returns true when it is on disk. Images and
        /// failed extractions return false. Never throws except for cancellation.
        /// </summary>
        Task<bool> EnsureThumbnailAsync(MediaItem media, CancellationToken ct);
    
        /// <summary>
        /// File to show as this item's thumbnail: the image itself for images, the stored frame for videos
        /// (generated now if it is missing). Null when no thumbnail can be produced.
        /// </summary>
        Task<string?> ResolveThumbnailPathAsync(MediaItem media, CancellationToken ct);
    
        /// <summary>
        /// Brings a video row in line with the disk: records the thumbnail file name when the file exists, and clears
        /// the stored name when the file is gone so the backfill picks the row up again. Saves only when something changed.
        /// </summary>
        Task ReconcileThumbnailAsync(MediaItem media, CancellationToken ct);
    }
}
