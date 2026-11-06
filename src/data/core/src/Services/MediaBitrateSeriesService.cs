using Microsoft.Extensions.Logging;

using System.Text.Json;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Computes a clip's per-second video bitrate from its packets the first time it is asked for, and saves the result on the
    /// clip. Later calls read the saved series, so the review page does not run ffprobe again and the clip keeps the figures it
    /// was reviewed with.
    /// </summary>
    public sealed class MediaBitrateSeriesService(
        IMediaItemRepository items,
        IMediaPacketReader packets,
        IMediaPathResolver paths,
        ILogger<MediaBitrateSeriesService> logger) : IMediaBitrateSeriesService
    {
        /// <inheritdoc />
        public async Task<IReadOnlyList<BitrateSecond>?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            MediaItem? media = await items.GetAsync(mediaItemId, ct);
            if (media is null)
            {
                return null;
            }

            if (media.BitrateSeriesJson is { } saved)
            {
                IReadOnlyList<BitrateSecond>? stored = JsonSerializer.Deserialize<List<BitrateSecond>>(saved);
                if (stored is not null)
                {
                    return stored;
                }
            }

            string absolute = await paths.ToAbsoluteAsync(media.FilePath, ct);
            IReadOnlyList<(double PtsSeconds, long SizeBytes)>? readPackets = await packets.ReadVideoPacketsAsync(absolute, ct);
            if (readPackets is null)
            {
                logger.LogError("Bit rate series failed for {FileName}: ffprobe could not read the packets", media.FileName);
                return null;
            }

            IReadOnlyList<BitrateSecond> series = BitrateSeries.FromPackets(readPackets);
            media.BitrateSeriesJson = JsonSerializer.Serialize(series);
            await items.UpdateAsync(media, ct);
            logger.LogInformation("Bit rate series saved for {FileName}: {Seconds} second(s)", media.FileName, series.Count);
            return series;
        }
    }
}
