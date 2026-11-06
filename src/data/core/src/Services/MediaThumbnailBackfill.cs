using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <inheritdoc />
    public sealed class MediaThumbnailBackfill : IMediaThumbnailBackfill
    {
        private readonly IMediaItemRepository _items;
        private readonly IMediaThumbnailService _thumbnails;
        private readonly ILogger<MediaThumbnailBackfill> _logger;
        private readonly IMediaPathResolver _paths;

        /// <summary>Creates the backfill.</summary>
        public MediaThumbnailBackfill(
            IMediaItemRepository items,
            IMediaThumbnailService thumbnails,
            ILogger<MediaThumbnailBackfill> logger,
            IMediaPathResolver? paths = null)
        {
            _items = items;
            _thumbnails = thumbnails;
            _logger = logger;
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        /// <inheritdoc />
        public async Task<MediaThumbnailBackfillResult> BackfillAsync(CancellationToken ct)
        {
            // The repository selects only videos with no ThumbnailPath, so the pass never scans the whole archive.
            IReadOnlyList<MediaItem> items = await _items.ListVideosWithoutThumbnailAsync(ct);

            int checkedCount = 0;
            int available = 0;
            int unavailable = 0;
            int missingFile = 0;

            foreach (MediaItem media in items)
            {
                ct.ThrowIfCancellationRequested();

                string absolutePath = await _paths.ToAbsoluteAsync(media.FilePath, ct);
                if (!File.Exists(absolutePath))
                {
                    // Why: a stored path with no file behind it means a row points at lost media, which needs review, not a skip.
                    _logger.LogError("Thumbnail backfill: media file missing at {Path}", absolutePath);
                    missingFile++;
                    continue;
                }

                checkedCount++;
                if (await _thumbnails.EnsureThumbnailAsync(media, ct))
                {
                    available++;
                }
                else
                {
                    unavailable++;
                }
            }

            if (missingFile > 0)
            {
                _logger.LogError(
                    "Thumbnail backfill: checked {Checked} video(s), {Available} with a thumbnail, {Unavailable} unavailable, {MissingFile} with no file on disk.",
                    checkedCount, available, unavailable, missingFile);
            }
            else
            {
                _logger.LogInformation(
                    "Thumbnail backfill: checked {Checked} video(s), {Available} with a thumbnail, {Unavailable} unavailable, {MissingFile} with no file on disk.",
                    checkedCount, available, unavailable, missingFile);
            }

            return new MediaThumbnailBackfillResult(checkedCount, available, unavailable, missingFile);
        }
    }
}
