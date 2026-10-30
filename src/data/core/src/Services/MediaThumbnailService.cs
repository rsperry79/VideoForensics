using Microsoft.Extensions.Logging;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <inheritdoc />
    public sealed class MediaThumbnailService : IMediaThumbnailService
    {
        /// <summary>Preferred frame: one second in skips a black or blank opening frame.</summary>
        private const long PreferredOffsetMs = 1000;

        private readonly IMediaFrameExtractor _frames;
        private readonly IMediaItemRepository _items;
        private readonly IMediaThumbnailNotifier _notifier;
        private readonly ILogger<MediaThumbnailService> _logger;
        private readonly IMediaPathResolver _paths;

        /// <summary>Creates the service.</summary>
        public MediaThumbnailService(
            IMediaFrameExtractor frames,
            IMediaItemRepository items,
            IMediaThumbnailNotifier notifier,
            ILogger<MediaThumbnailService> logger,
            IMediaPathResolver? paths = null)
        {
            _frames = frames;
            _items = items;
            _notifier = notifier;
            _logger = logger;
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        /// <summary>
        /// File name of a clip's thumbnail: the stem plus ".thumb.png". It is stored on the row relative to the clip's
        /// folder, so the folder comes from the clip and the archive can move without rewriting rows.
        /// </summary>
        public static string ThumbnailFileNameFor(string mediaFilePath)
        {
            return Path.GetFileNameWithoutExtension(mediaFilePath) + ".thumb.png";
        }

        /// <summary>Full path of the thumbnail beside the clip.</summary>
        public static string ThumbnailPathFor(string mediaFilePath)
        {
            string directory = Path.GetDirectoryName(mediaFilePath) ?? string.Empty;
            return Path.Combine(directory, ThumbnailFileNameFor(mediaFilePath));
        }

        /// <inheritdoc />
        public async Task ReconcileThumbnailAsync(MediaItem media, CancellationToken ct)
        {
            if (!IsVideo(media.MediaFormat))
            {
                return;
            }

            if (File.Exists(ThumbnailPathFor(await _paths.ToAbsoluteAsync(media.FilePath, ct))))
            {
                await RecordPathAsync(media, ct);
                return;
            }

            if (media.ThumbnailPath is not null)
            {
                media.ThumbnailPath = null;
                await _items.UpdateAsync(media, ct);
            }
        }

        /// <summary>Stores the file name on the row when it is missing or stale; saves only on a change.</summary>
        private async Task RecordPathAsync(MediaItem media, CancellationToken ct)
        {
            string fileName = ThumbnailFileNameFor(media.FilePath);
            if (media.ThumbnailPath == fileName)
            {
                return;
            }

            media.ThumbnailPath = fileName;
            await _items.UpdateAsync(media, ct);
        }

        /// <inheritdoc />
        public async Task<string?> ResolveThumbnailPathAsync(MediaItem media, CancellationToken ct)
        {
            string source = await _paths.ToAbsoluteAsync(media.FilePath, ct);
            return !IsVideo(media.MediaFormat) ? source : await EnsureThumbnailAsync(media, ct) ? ThumbnailPathFor(source) : null;
        }

        /// <inheritdoc />
        public async Task<bool> EnsureThumbnailAsync(MediaItem media, CancellationToken ct)
        {
            if (!IsVideo(media.MediaFormat))
            {
                return false;
            }

            string source = await _paths.ToAbsoluteAsync(media.FilePath, ct);
            string thumbnail = ThumbnailPathFor(source);
            if (File.Exists(thumbnail))
            {
                await RecordPathAsync(media, ct);
                return true;
            }

            try
            {
                byte[]? png = await ExtractFirstFrameAsync(source, ct);
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
                await RecordPathAsync(media, ct);
                await AnnounceAsync(media.Id, ct);
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

        /// <summary>
        /// Why: the thumbnail is already saved when this runs, so a failed announcement must not make generation fail
        /// (the next pass would then wrongly regenerate it). The failure is logged, and open views simply refresh later.
        /// </summary>
        private async Task AnnounceAsync(Guid mediaItemId, CancellationToken ct)
        {
            try
            {
                await _notifier.ThumbnailReadyAsync(mediaItemId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not announce thumbnail for media item {MediaItemId}", mediaItemId);
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
        internal static bool IsVideo(string? format)
        {
            return !string.IsNullOrEmpty(format) && (
                format.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
                format.Contains("video", StringComparison.OrdinalIgnoreCase));
        }
    }
}
