using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Events;
using VideoForensics.Ui.Shared.Services.Scope;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Loads a unified evidence stream: events and media files for a forensic scope, de-duplicated.
/// </summary>
public class EvidenceLoader
{
    private readonly EventRowLoader _eventRowLoader;
    private readonly IMediaItemRepository _mediaItemRepository;

    public EvidenceLoader(EventRowLoader eventRowLoader, IMediaItemRepository mediaItemRepository)
    {
        _eventRowLoader = eventRowLoader;
        _mediaItemRepository = mediaItemRepository;
    }

    /// <summary>
    /// Load all evidence (events and media) for the given scope.
    /// </summary>
    /// <param name="scope">The forensic scope (device IDs, date range, search query).</param>
    /// <param name="allDevices">All available devices (used to resolve device names).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An EvidenceLoadResult with merged items, holds, integrity records, and errors.</returns>
    public async Task<EvidenceLoadResult> LoadAsync(
        ForensicScope scope,
        IReadOnlyList<Device> allDevices,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> allDeviceIds = scope.IncludesAllDevices
            ? allDevices.Select(d => d.Id).ToList()
            : scope.DeviceIds;

        var devicesByIdMap = allDevices.ToDictionary(d => d.Id);
        var allItems = new List<EvidenceItem>();
        var perDeviceErrors = new Dictionary<Guid, string>();
        var activeHoldsByMediaItemId = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();
        var mediaItemsAttachedToEvents = new HashSet<Guid>();

        // Step 1: Load events
        EventLoadResult eventLoadResult = await _eventRowLoader.LoadAsync(scope, allDeviceIds, false, ct);

        // Add event items and collect attached media
        foreach (EventRow row in eventLoadResult.Rows)
        {
            string deviceName = devicesByIdMap.TryGetValue(row.DeviceId, out Device? device)
                ? device.Name
                : row.DeviceId.ToString("D")[..8];

            var item = new EvidenceItem
            {
                Key = $"event:{row.Event.Id}",
                Kind = EvidenceKind.Event,
                OccurredAtUtc = row.OccurredAtUtc,
                DeviceId = row.DeviceId,
                DeviceName = deviceName,
                Event = row.Event,
                Media = row.MediaItem
            };

            allItems.Add(item);

            if (row.MediaItem is not null)
            {
                _ = mediaItemsAttachedToEvents.Add(row.MediaItem.Id);
            }
        }

        // Merge event holds and integrity records
        foreach (KeyValuePair<Guid, LegalHold> kvp in eventLoadResult.ActiveHoldsByMediaItemId)
        {
            activeHoldsByMediaItemId[kvp.Key] = kvp.Value;
        }

        foreach (IntegrityRecord record in eventLoadResult.IntegrityRecords)
        {
            if (!integrityRecords.Any(r => r.Id == record.Id))
            {
                integrityRecords.Add(record);
            }
        }

        foreach (KeyValuePair<Guid, string> kvp in eventLoadResult.PerDeviceErrors)
        {
            perDeviceErrors[kvp.Key] = kvp.Value;
        }

        // Step 2: Load media per device and add media-only items
        foreach (Guid deviceId in allDeviceIds)
        {
            try
            {
                IReadOnlyList<MediaItem> mediaItems = await _mediaItemRepository.GetByDeviceAndDateRangeAsync(
                    deviceId,
                    scope.FromUtc,
                    scope.ToUtc,
                    ct);

                string deviceName = devicesByIdMap.TryGetValue(deviceId, out Device? device)
                    ? device.Name
                    : deviceId.ToString("D")[..8];

                foreach (MediaItem media in mediaItems)
                {
                    // Skip purged media
                    if (media.IsPurged)
                    {
                        continue;
                    }

                    // Skip media already attached to an event
                    if (mediaItemsAttachedToEvents.Contains(media.Id))
                    {
                        continue;
                    }

                    // Classify by format
                    EvidenceKind kind = ClassifyMediaKind(media.MediaFormat);

                    var item = new EvidenceItem
                    {
                        Key = $"media:{media.Id}",
                        Kind = kind,
                        OccurredAtUtc = media.RecordedAtUtc,
                        DeviceId = deviceId,
                        DeviceName = deviceName,
                        Event = null,
                        Media = media
                    };

                    allItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                perDeviceErrors[deviceId] = ex.Message;
            }
        }

        // Step 3: Apply search filter to media-only items
        if (!string.IsNullOrWhiteSpace(scope.Search))
        {
            string searchLower = scope.Search.ToLowerInvariant();
            allItems = allItems
                .Where(i =>
                {
                    // Events are already filtered by EventRowLoader
                    if (i.Kind == EvidenceKind.Event)
                    {
                        return true;
                    }

                    // Media-only items: match by FileName, Label, and DeviceName
                    return (i.Media?.FileName ?? string.Empty).Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                           i.Label.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                           i.DeviceName.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }

        // Step 4: Sort by OccurredAtUtc descending
        allItems = allItems
            .OrderByDescending(i => i.OccurredAtUtc)
            .ToList();

        return new EvidenceLoadResult
        {
            Items = allItems,
            ActiveHoldsByMediaItemId = activeHoldsByMediaItemId,
            IntegrityRecords = integrityRecords,
            Errors = perDeviceErrors
        };
    }

    private static EvidenceKind ClassifyMediaKind(string mediaFormat)
    {
        return string.IsNullOrEmpty(mediaFormat)
            ? EvidenceKind.File
            : mediaFormat.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            mediaFormat.Contains("jpg", StringComparison.OrdinalIgnoreCase) ||
            mediaFormat.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            mediaFormat.Contains("png", StringComparison.OrdinalIgnoreCase)
            ? EvidenceKind.Snapshot
            : mediaFormat.Contains("video", StringComparison.OrdinalIgnoreCase) ||
            mediaFormat.Contains("mp4", StringComparison.OrdinalIgnoreCase)
            ? EvidenceKind.Video
            : EvidenceKind.File;
    }
}
