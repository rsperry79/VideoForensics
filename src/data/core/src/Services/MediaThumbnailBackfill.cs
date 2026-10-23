using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Outcome of one backfill pass over the media archive.</summary>
    /// <param name="Checked">Videos with a file on disk that were checked for a thumbnail.</param>
    /// <param name="Available">Checked videos that have a thumbnail after the pass.</param>
    /// <param name="Unavailable">Checked videos whose thumbnail could not be made.</param>
    /// <param name="MissingFile">Videos whose media file is gone from disk, so no thumbnail is possible.</param>
    public sealed record MediaThumbnailBackfillResult(int Checked, int Available, int Unavailable, int MissingFile);

    /// <summary>Generates thumbnails for archived videos that do not have one yet.</summary>
    public interface IMediaThumbnailBackfill
    {
        /// <summary>Checks every non-purged video and ensures it has a thumbnail.</summary>
        Task<MediaThumbnailBackfillResult> BackfillAsync(CancellationToken ct);
    }

    /// <inheritdoc />
    public sealed class MediaThumbnailBackfill : IMediaThumbnailBackfill
    {
        private readonly IMediaItemRepository _items;
        private readonly IMediaThumbnailService _thumbnails;
        private readonly ILogger<MediaThumbnailBackfill> _logger;

        /// <summary>Creates the backfill.</summary>
        public MediaThumbnailBackfill(
            IMediaItemRepository items,
            IMediaThumbnailService thumbnails,
            ILogger<MediaThumbnailBackfill> logger)
        {
            _items = items;
            _thumbnails = thumbnails;
            _logger = logger;
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

                if (!File.Exists(media.FilePath))
                {
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

            _logger.LogInformation(
                "Thumbnail backfill: checked {Checked} video(s), {Available} with a thumbnail, {Unavailable} unavailable, {MissingFile} with no file on disk.",
                checkedCount, available, unavailable, missingFile);

            return new MediaThumbnailBackfillResult(checkedCount, available, unavailable, missingFile);
        }
    }
}
