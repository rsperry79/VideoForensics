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
    public static class EventBackfillService
    {
        public static async Task<int> BackfillFromDownloadEventsAsync(
            IDownloadEventRepository downloadEventRepository,
            IMediaItemRepository mediaItemRepository,
            IEventRepository eventRepository,
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

        /// <summary>
        /// Returns the id to store. Numeric ids are truncated doorbot ids unless they already match a string ding id on the
        /// device; the prefix match is written only when exactly one candidate matches. Null means skip the row.
        /// </summary>
        private static async Task<string?> ResolveProviderEventIdAsync(
            DownloadEvent downloadEvent,
            IUnitOfWork unitOfWork,
            Dictionary<Guid, IReadOnlyList<string>> candidatesByDevice,
            CancellationToken ct)
        {
            string id = downloadEvent.ProviderEventId;
            // Non-numeric ids (snapshot-*, legacy ids such as ring_evt_001) are not doorbot ids, so they stay as they are.
            if (!id.All(char.IsAsciiDigit))
            {
                return id;
            }

            if (!candidatesByDevice.TryGetValue(downloadEvent.DeviceId, out IReadOnlyList<string>? candidates))
            {
                candidates = await unitOfWork.ExecuteAsync(async ctx =>
                {
                    Device? device = await ctx.Devices.GetAsync(downloadEvent.DeviceId, ct);
                    if (device is null || !long.TryParse(device.ProviderDeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out long doorbotId))
                    {
                        return (IReadOnlyList<string>)Array.Empty<string>();
                    }

                    return await ctx.DingIdLinks.GetCandidateDingIdsAsync(doorbotId, ct);
                }, ct);
                candidatesByDevice[downloadEvent.DeviceId] = candidates;
            }

            if (candidates.Contains(id, StringComparer.Ordinal))
            {
                return id;
            }

            DingIdMatchResult match = DingIdPrefixMatcher.Match(id, candidates);
            return match.Outcome == DingIdMatchOutcome.Matched ? match.DingId : null;
        }
    }
}
