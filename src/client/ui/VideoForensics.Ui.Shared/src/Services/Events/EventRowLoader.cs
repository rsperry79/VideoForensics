using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Common.Contracts;
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
        var devicesToLoad = scope.IncludesAllDevices ? allDeviceIds : scope.DeviceIds;
        var allRows = new List<EventRow>();
        var perDeviceErrors = new Dictionary<Guid, string>();
        var activeHoldsByMediaItemId = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Load events per device
        foreach (var deviceId in devicesToLoad)
        {
            try
            {
                IReadOnlyList<Event> events;

                if (onlyUnansweredOrFlagged)
                {
                    events = await _eventRepository.ListUnansweredOrFlaggedAsync(deviceId, ct);
                }
                else
                {
                    events = await _eventRepository.ListByDeviceAndDateRangeAsync(
                        deviceId,
                        scope.FromUtc,
                        scope.ToUtc,
                        ct);
                }

                var rows = events.Select(e => new EventRow { Event = e, DeviceId = deviceId }).ToList();

                // For date-range queries, fetch media and holds
                if (!onlyUnansweredOrFlagged && events.Count > 0)
                {
                    var report = await _reportGenerationService.BuildEvidenceReviewAsync(
                        deviceId,
                        scope.FromUtc,
                        scope.ToUtc,
                        ct);

                    var mediaByDownloadEventId = report.MediaItems
                        .Where(m => m.DownloadEventId.HasValue)
                        .ToDictionary(m => m.DownloadEventId!.Value);

                    foreach (var row in rows)
                    {
                        if (mediaByDownloadEventId.TryGetValue(row.Event.Id, out var mediaItem))
                        {
                            row.MediaItem = mediaItem;
                        }
                    }

                    // Collect integrity records from all devices
                    foreach (var record in report.IntegrityRecords)
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
            var searchLower = scope.Search.ToLowerInvariant();
            allRows = allRows
                .Where(r =>
                    r.EventType.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    r.ProviderEventId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    (r.MediaItem?.FileName ?? "").Contains(searchLower, StringComparison.OrdinalIgnoreCase))
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
                var activeHolds = await _legalHoldRepository.GetActiveByMediaItemIdsAsync(mediaItemIds, ct);
                foreach (var hold in activeHolds)
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
