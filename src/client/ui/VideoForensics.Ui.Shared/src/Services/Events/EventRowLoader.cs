using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Ui.Shared.Services.Scope;

namespace VideoForensics.Ui.Shared.Services.Events;

/// <summary>
/// Loads event rows for a forensic scope, handling device fan-out, merging, searching, and legal holds.
/// </summary>
public class EventRowLoader
{
    private readonly IEventRepository _eventRepository;
    private readonly IReportGenerationService _reportGenerationService;
    private readonly ILegalHoldRepository _legalHoldRepository;

    public EventRowLoader(
        IEventRepository eventRepository,
        IReportGenerationService reportGenerationService,
        ILegalHoldRepository legalHoldRepository)
    {
        _eventRepository = eventRepository;
        _reportGenerationService = reportGenerationService;
        _legalHoldRepository = legalHoldRepository;
    }

    /// <summary>
    /// Load events for the given forensic scope, applying search filters and assembling metadata.
    /// </summary>
    /// <param name="scope">The forensic scope (device IDs, date range, search query).</param>
    /// <param name="allDeviceIds">All available device IDs (used when scope includes all devices).</param>
    /// <param name="onlyUnansweredOrFlagged">If true, load only unanswered/flagged events and skip evidence review join.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An EventLoadResult with rows, holds, integrity records, and per-device errors.</returns>
    public async Task<EventLoadResult> LoadAsync(
        ForensicScope scope,
        IReadOnlyList<Guid> allDeviceIds,
        bool onlyUnansweredOrFlagged,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> devicesToLoad = scope.IncludesAllDevices ? allDeviceIds : scope.DeviceIds;
        var allRows = new List<EventRow>();
        var perDeviceErrors = new Dictionary<Guid, string>();
        var activeHoldsByMediaItemId = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Load events per device
        foreach (Guid deviceId in devicesToLoad)
        {
            try
            {
                IReadOnlyList<Event> events = onlyUnansweredOrFlagged
                    ? await _eventRepository.ListUnansweredOrFlaggedAsync(deviceId, ct)
                    : await _eventRepository.ListByDeviceAndDateRangeAsync(
                        deviceId,
                        scope.FromUtc,
                        scope.ToUtc,
                        ct);

                var rows = events.Select(e => new EventRow { Event = e, DeviceId = deviceId }).ToList();

                // For date-range queries, fetch media and holds
                if (!onlyUnansweredOrFlagged && events.Count > 0)
                {
                    EvidenceReviewReport report = await _reportGenerationService.BuildEvidenceReviewAsync(
                        deviceId,
                        scope.FromUtc,
                        scope.ToUtc,
                        ct);

                    // Media is joined to events by ProviderEventId, not Event.Id: the report's links carry the
                    // provider event id per media item, and Event and DownloadEvent share ProviderEventId per device.
                    var providerEventIdByMediaItemId = new Dictionary<Guid, string>();
                    foreach (MediaProviderEventLink link in report.MediaProviderEventLinks)
                    {
                        _ = providerEventIdByMediaItemId.TryAdd(link.MediaItemId, link.ProviderEventId);
                    }

                    var mediaByProviderEventId = new Dictionary<string, MediaItem>();
                    foreach (MediaItem media in report.MediaItems)
                    {
                        if (providerEventIdByMediaItemId.TryGetValue(media.Id, out string? providerEventId))
                        {
                            _ = mediaByProviderEventId.TryAdd(providerEventId, media);
                        }
                    }

                    foreach (EventRow? row in rows)
                    {
                        if (mediaByProviderEventId.TryGetValue(row.Event.ProviderEventId, out MediaItem? mediaItem))
                        {
                            row.MediaItem = mediaItem;
                        }
                    }

                    // Collect integrity records from all devices
                    foreach (IntegrityRecord record in report.IntegrityRecords)
                    {
                        if (!integrityRecords.Any(r => r.Id == record.Id))
                        {
                            integrityRecords.Add(record);
                        }
                    }
                }

                allRows.AddRange(rows);
            }
            catch (Exception ex)
            {
                perDeviceErrors[deviceId] = ex.Message;
            }
        }

        // Apply search filter (case-insensitive substring matching)
        if (!string.IsNullOrWhiteSpace(scope.Search))
        {
            string searchLower = scope.Search.ToLowerInvariant();
            allRows = allRows
                .Where(r =>
                    r.EventType.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    r.ProviderEventId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    (r.MediaItem?.FileName ?? string.Empty).Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Sort by OccurredAtUtc descending
        allRows = allRows
            .OrderByDescending(r => r.OccurredAtUtc)
            .ToList();

        // Load legal holds for all media items (only in non-unanswered mode)
        if (!onlyUnansweredOrFlagged)
        {
            var mediaItemIds = allRows.Where(r => r.MediaItem is not null).Select(r => r.MediaItem!.Id).ToList();
            if (mediaItemIds.Count > 0)
            {
                IReadOnlyList<LegalHold> activeHolds = await _legalHoldRepository.GetActiveByMediaItemIdsAsync(mediaItemIds, ct);
                foreach (LegalHold hold in activeHolds)
                {
                    activeHoldsByMediaItemId[hold.MediaItemId] = hold;
                }
            }
        }

        return new EventLoadResult
        {
            Rows = allRows,
            ActiveHoldsByMediaItemId = activeHoldsByMediaItemId,
            IntegrityRecords = integrityRecords,
            PerDeviceErrors = perDeviceErrors
        };
    }
}
