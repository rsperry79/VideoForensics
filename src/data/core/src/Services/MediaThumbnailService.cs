using Microsoft.Extensions.Logging;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
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
    }

    /// <inheritdoc />
    public sealed class MediaThumbnailService : IMediaThumbnailService
    {
        /// <summary>Preferred frame: one second in skips a black or blank opening frame.</summary>
        private const long PreferredOffsetMs = 1000;

        private readonly IMediaFrameExtractor _frames;
        private readonly ILogger<MediaThumbnailService> _logger;

        /// <summary>Creates the service.</summary>
        public MediaThumbnailService(IMediaFrameExtractor frames, ILogger<MediaThumbnailService> logger)
        {
            _frames = frames;
            _logger = logger;
        }

        /// <summary>
        /// Path of the thumbnail sidecar, next to the media file. A sidecar keeps thumbnails out of the schema:
        /// the file is the only record, so a missing one is found by checking the disk.
        /// </summary>
        public static string ThumbnailPathFor(string mediaFilePath)
        {
            string directory = Path.GetDirectoryName(mediaFilePath) ?? string.Empty;
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(mediaFilePath) + ".thumb.png");
        }

        /// <inheritdoc />
        public async Task<string?> ResolveThumbnailPathAsync(MediaItem media, CancellationToken ct)
        {
            if (!IsVideo(media.MediaFormat))
            {
                return media.FilePath;
            }

            return await EnsureThumbnailAsync(media, ct) ? ThumbnailPathFor(media.FilePath) : null;
        }

        /// <inheritdoc />
        public async Task<bool> EnsureThumbnailAsync(MediaItem media, CancellationToken ct)
        {
            if (!IsVideo(media.MediaFormat))
            {
                return false;
            }

            string thumbnail = ThumbnailPathFor(media.FilePath);
            if (File.Exists(thumbnail))
            {
                return true;
            }

            try
            {
                byte[]? png = await ExtractFirstFrameAsync(media.FilePath, ct);
                if (png is null)
                {
                    _logger.LogWarning("No thumbnail frame could be extracted for media item {MediaItemId}", media.Id);
                    return false;
                }

                // Write beside the target and move it into place, so a crash never leaves a truncated thumbnail
                // that the existence check would then treat as done.
                string temp = thumbnail + ".tmp";
                await File.WriteAllBytesAsync(temp, png, ct);
                File.Move(temp, thumbnail, overwrite: true);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thumbnail generation failed for media item {MediaItemId}", media.Id);
                return false;
            }
        }

        private async Task<byte[]?> ExtractFirstFrameAsync(string mediaFilePath, CancellationToken ct)
        {
            // Offset 0 is the fallback for clips shorter than the preferred offset.
            foreach (long offsetMs in new[] { PreferredOffsetMs, 0 })
            {
                FrameExtractionResult result = await _frames.ExtractFrameAsync(mediaFilePath, offsetMs, ct);
                if (result.Success && result.PngBytes is { Length: > 0 } png)
                {
                    return png;
                }
            }

            return null;
        }

        /// <summary>Same rule the UI uses to pick a thumbnail source: the format name contains "video" or "mp4".</summary>
        internal static bool IsVideo(string? format) =>
            !string.IsNullOrEmpty(format) && (
                format.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
                format.Contains("video", StringComparison.OrdinalIgnoreCase));
    }
}
