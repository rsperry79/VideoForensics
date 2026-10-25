using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <inheritdoc />
    public sealed class MediaPreviewBackfill : IMediaPreviewBackfill
    {
        private readonly IMediaItemRepository _items;
        private readonly IMediaPreviewService _previews;
        private readonly ILogger<MediaPreviewBackfill> _logger;

        /// <summary>Creates the backfill.</summary>
        public MediaPreviewBackfill(
            IMediaItemRepository items,
            IMediaPreviewService previews,
            ILogger<MediaPreviewBackfill> logger)
        {
            _items = items;
            _previews = previews;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<MediaPreviewBackfillResult> RunAsync(int batchSize, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

            // Why: a clip that fails keeps a null PreviewPath, so the repository returns it again at the head of every batch.
            // Each batch asks for batchSize plus the clips already attempted, so the fresh clips behind them stay reachable,
            // and a clip is only ever attempted once per run.
            var attempted = new HashSet<Guid>();
            int generated = 0;
            int failed = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                IReadOnlyList<MediaItem> batch = await _items.ListVideosWithoutPreviewAsync(batchSize + attempted.Count, ct);

                int fresh = 0;
                foreach (MediaItem media in batch)
                {
                    if (!attempted.Add(media.Id))
                    {
                        continue;
                    }

                    fresh++;
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        if (await _previews.EnsurePreviewAsync(media, ct))
                        {
                            generated++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Preview backfill failed for {FileName}", media.FileName);
                    }
                }

                // Stop when the repository has nothing new: an empty batch, or only clips already attempted this run.
                if (fresh == 0)
                {
                    break;
                }
            }

            return new MediaPreviewBackfillResult(generated + failed, generated, failed);
        }
    }
}
