using System.Globalization;

using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Matching;

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
    public class EventBackfillService
    {
        private readonly IDeviceWriteRepository _devices;
        private readonly IDingIdLinkRepository _dingIdLinks;

        /// <summary>Initializes a new instance of the EventBackfillService.</summary>
        public EventBackfillService(IDeviceWriteRepository devices, IDingIdLinkRepository dingIdLinks)
        {
            _devices = devices;
            _dingIdLinks = dingIdLinks;
        }

        /// <summary>Reconstructs Event rows from DownloadEvents and MediaItems. Returns the number of Events written.</summary>
        public async Task<int> BackfillFromDownloadEventsAsync(
            IDownloadEventRepository downloadEventRepository,
            IMediaItemWriteRepository mediaItemRepository,
            IEventWriteRepository eventRepository,
            IUnitOfWork unitOfWork,
            ILogger logger,
            CancellationToken ct)
        {
            IReadOnlyList<DownloadEvent> downloadEvents = await downloadEventRepository.ListAsync(ct);
            if (downloadEvents.Count == 0)
            {
                logger.LogDebug("Events backfill: no DownloadEvents found, nothing to backfill.");
                return 0;
            }

            IReadOnlyList<MediaItem> mediaItems = await mediaItemRepository.ListAsync(ct);
            var hashByDownloadEventId = mediaItems
                .Where(m => m.DownloadEventId.HasValue)
                .GroupBy(m => m.DownloadEventId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Sha256Hash);

            var candidatesByDevice = new Dictionary<Guid, IReadOnlyList<string>>();
            int skipped = 0;
            int backfilled = 0;
            foreach (DownloadEvent downloadEvent in downloadEvents)
            {
                ct.ThrowIfCancellationRequested();

                _ = hashByDownloadEventId.TryGetValue(downloadEvent.Id, out string? hash);

                string? providerEventId = await ResolveProviderEventIdAsync(downloadEvent, unitOfWork, candidatesByDevice, ct);
                if (providerEventId is null)
                {
                    skipped++;
                    continue;
                }

                _ = await eventRepository.UpsertAsync(new Event
                {
                    Id = Guid.NewGuid(),
                    DeviceId = downloadEvent.DeviceId,
                    ProviderEventId = providerEventId,
                    EventType = downloadEvent.EventType ?? "unknown",
                    OccurredAtUtc = downloadEvent.EventOccurredAtUtc,
                    DiscoveredAtUtc = downloadEvent.DownloadStartedUtc,
                    DownloadedAtUtc = downloadEvent.Success ? downloadEvent.DownloadCompletedUtc : null,
                    EventIntegrityHash = hash
                }, ct);

                backfilled++;
            }

            logger.LogInformation("Events backfill: reconstructed {Count} Event records from existing DownloadEvents; skipped {Skipped} whose id did not resolve to one ding id.", backfilled, skipped);
            return backfilled;
        }

        /// <summary>Returns the id to store for a DownloadEvent, or null when the row cannot be resolved to one ding id.</summary>
        private async Task<string?> ResolveProviderEventIdAsync(
            DownloadEvent downloadEvent,
            IUnitOfWork unitOfWork,
            Dictionary<Guid, IReadOnlyList<string>> candidatesByDevice,
            CancellationToken ct)
        {
            if (!candidatesByDevice.TryGetValue(downloadEvent.DeviceId, out IReadOnlyList<string>? candidates))
            {
                candidates = await unitOfWork.ExecuteAsync(async _ =>
                {
                    Device? device = await _devices.GetAsync(downloadEvent.DeviceId, ct);
                    return await ProviderEventIdResolver.CandidatesForDeviceAsync(device, _dingIdLinks, ct);
                }, ct);
                candidatesByDevice[downloadEvent.DeviceId] = candidates;
            }

            return ProviderEventIdResolver.Resolve(downloadEvent.ProviderEventId, candidates);
        }
    }
}
