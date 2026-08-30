using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// One-time backfill: reconstructs Events-table rows from the existing DownloadEvents/MediaItems
    /// history. The Events table (independent of download status) is what all forensic Timeline/
    /// Integrity/Correlation/Audit queries read from, but historically nothing wrote to it — only
    /// DownloadEvents/MediaItems were populated by the download pipeline. This recovers what it can
    /// from that history; events that were discovered but never downloaded (and so never appear in
    /// DownloadEvents at all) cannot be recovered by this backfill.
    /// </summary>
    public static class EventBackfillService
    {
        public static async Task<int> BackfillFromDownloadEventsAsync(
            IDownloadEventRepository downloadEventRepository,
            IMediaItemRepository mediaItemRepository,
            IEventRepository eventRepository,
            ILogger logger,
            CancellationToken ct)
        {
            var downloadEvents = await downloadEventRepository.ListAsync(ct);
            if (downloadEvents.Count == 0)
            {
                logger.LogInformation("Events backfill: no DownloadEvents found, nothing to backfill.");
                return 0;
            }

            var mediaItems = await mediaItemRepository.ListAsync(ct);
            var hashByDownloadEventId = mediaItems
                .Where(m => m.DownloadEventId.HasValue)
                .GroupBy(m => m.DownloadEventId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Sha256Hash);

            var backfilled = 0;
            foreach (var downloadEvent in downloadEvents)
            {
                ct.ThrowIfCancellationRequested();

                hashByDownloadEventId.TryGetValue(downloadEvent.Id, out var hash);

                await eventRepository.UpsertAsync(new Event
                {
                    Id = Guid.NewGuid(),
                    DeviceId = downloadEvent.DeviceId,
                    ProviderEventId = downloadEvent.ProviderEventId,
                    EventType = downloadEvent.EventType ?? "unknown",
                    OccurredAtUtc = downloadEvent.EventOccurredAtUtc,
                    DiscoveredAtUtc = downloadEvent.DownloadStartedUtc,
                    DownloadedAtUtc = downloadEvent.Success ? downloadEvent.DownloadCompletedUtc : null,
                    EventIntegrityHash = hash
                }, ct);

                backfilled++;
            }

            logger.LogInformation("Events backfill: reconstructed {Count} Event records from existing DownloadEvents.", backfilled);
            return backfilled;
        }
    }
}
