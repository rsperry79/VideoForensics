using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Runs ffprobe only, to fill the codec, audio codec, resolution and frame rate of rows written before those fields existed.
    /// Leaves flags, baselines and the other analysis results alone. An item ffprobe cannot read is skipped for the rest of the
    /// process lifetime, so it does not block the batch; a restart retries it.
    /// </summary>
    public sealed class MediaCodecBackfill : IMediaCodecBackfill
    {
        /// <summary>Items per pass.</summary>
        public const int BatchSize = 50;

        private readonly IMediaItemWriteRepository _items;
        private readonly IMediaBitrateAnalyzer _bitrate;
        private readonly IMediaPathResolver _paths;
        private readonly ILogger<MediaCodecBackfill> _logger;
        private readonly HashSet<Guid> _skipped = [];

        public MediaCodecBackfill(IMediaItemWriteRepository items, IMediaBitrateAnalyzer bitrate, IMediaPathResolver? paths, ILogger<MediaCodecBackfill> logger)
        {
            _items = items;
            _bitrate = bitrate;
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<int> BackfillAsync(CancellationToken ct)
        {
            // Fetch past the skipped rows so a full batch of unreadable files does not stall the pass.
            IReadOnlyList<MediaItem> candidates = await _items.ListMissingCodecAsync(BatchSize + _skipped.Count, ct);
            int filled = 0;
            foreach (MediaItem item in candidates.Where(i => !_skipped.Contains(i.Id)).Take(BatchSize))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    string path = await _paths.ToAbsoluteAsync(item.FilePath, ct);
                    MediaBitrateAnalysis? probe = await _bitrate.AnalyzeAsync(path, item.FileSizeBytes, ct);
                    if (probe?.VideoCodec is null)
                    {
                        _ = _skipped.Add(item.Id);
                        continue;
                    }

                    item.VideoCodec = probe.VideoCodec;
                    item.AudioCodec = probe.AudioCodec;
                    item.Resolution = probe.Resolution;
                    item.FrameRate = probe.FrameRate;
                    await _items.UpdateAsync(item, ct);
                    filled++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _ = _skipped.Add(item.Id);
                    _logger.LogWarning("Codec backfill could not read {FileName}; skipped until restart: {Message} ({ErrorType})", item.FileName, ex.Message, ex.GetType().Name);
                }
            }

            if (filled > 0)
            {
                _logger.LogInformation("Codec backfill filled {Filled} media item(s)", filled);
            }

            return filled;
        }
    }
}
