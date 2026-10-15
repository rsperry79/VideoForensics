namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Groups evidence items by day and device for timeline/gallery views.
/// </summary>
public static class EvidenceTimeline
{
    /// <summary>
    /// Groups evidence items by local day (in the given timezone), then by device within each day.
    /// Returns days in descending order (most recent first).
    /// </summary>
    /// <param name="items">The evidence items, assumed to be sorted by OccurredAtUtc descending.</param>
    /// <param name="displayZone">The timezone to use for grouping (e.g., user's local timezone).</param>
    /// <returns>Groups of days, each containing device groups.</returns>
    public static IReadOnlyList<EvidenceDayGroup> Group(
        IEnumerable<EvidenceItem> items,
        TimeZoneInfo displayZone)
    {
        var itemsList = items.ToList();
        var dayGroups = new Dictionary<DateOnly, Dictionary<Guid, List<EvidenceItem>>>();

        foreach (var item in itemsList)
        {
            // Convert UTC to local time in displayZone
            var localTime = TimeZoneInfo.ConvertTime(
                new DateTime(item.OccurredAtUtc.Ticks, DateTimeKind.Utc),
                TimeZoneInfo.Utc,
                displayZone);

            var localDay = DateOnly.FromDateTime(localTime);

            if (!dayGroups.TryGetValue(localDay, out var deviceGroups))
            {
                deviceGroups = new Dictionary<Guid, List<EvidenceItem>>();
                dayGroups[localDay] = deviceGroups;
            }

            if (!deviceGroups.TryGetValue(item.DeviceId, out var deviceItems))
            {
                deviceItems = new List<EvidenceItem>();
                deviceGroups[item.DeviceId] = deviceItems;
            }

            deviceItems.Add(item);
        }

        // Build the result, sorted by day descending
        var result = new List<EvidenceDayGroup>();
        foreach (var dayKvp in dayGroups.OrderByDescending(x => x.Key))
        {
            var deviceGroupList = new List<EvidenceDeviceGroup>();
            foreach (var deviceKvp in dayKvp.Value.OrderBy(x =>
            {
                // Find device name from first item in this device group
                var firstItem = x.Value.First();
                return firstItem.DeviceName;
            }))
            {
                // Sort items within device group by OccurredAtUtc descending
                var sortedItems = deviceKvp.Value.OrderByDescending(i => i.OccurredAtUtc).ToList();

                var deviceGroup = new EvidenceDeviceGroup(
                    DeviceId: deviceKvp.Key,
                    DeviceName: deviceKvp.Value.First().DeviceName,
                    Items: sortedItems.AsReadOnly());

                deviceGroupList.Add(deviceGroup);
            }

            var totalCount = dayKvp.Value.Values.Sum(items => items.Count);
            var dayGroup = new EvidenceDayGroup(
                Day: dayKvp.Key,
                Devices: deviceGroupList.AsReadOnly(),
                TotalCount: totalCount);

            result.Add(dayGroup);
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Finds the previous and next items in an ordered list by key.
    /// Previous = the item before (newer), Next = the item after (older).
    /// When viewableOnly is true, skips items without viewable media.
    /// </summary>
    /// <param name="ordered">The ordered list (assumed descending by OccurredAtUtc).</param>
    /// <param name="key">The key of the current item.</param>
    /// <param name="viewableOnly">If true, only consider items with viewable media.</param>
    /// <returns>A tuple of (previous, next) items.</returns>
    public static (EvidenceItem? Previous, EvidenceItem? Next) Neighbours(
        IReadOnlyList<EvidenceItem> ordered,
        string key,
        bool viewableOnly)
    {
        var currentIndex = -1;
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Key == key)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return (null, null);
        }

        EvidenceItem? previous = null;
        EvidenceItem? next = null;

        // Find previous (earlier index = newer time)
        if (currentIndex > 0)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (!viewableOnly || ordered[i].HasViewableMedia)
                {
                    previous = ordered[i];
                    break;
                }
            }
        }

        // Find next (later index = older time)
        if (currentIndex < ordered.Count - 1)
        {
            for (int i = currentIndex + 1; i < ordered.Count; i++)
            {
                if (!viewableOnly || ordered[i].HasViewableMedia)
                {
                    next = ordered[i];
                    break;
                }
            }
        }

        return (previous, next);
    }
}

/// <summary>
/// Groups evidence items by day, containing device groups.
/// </summary>
public record EvidenceDayGroup(
    DateOnly Day,
    IReadOnlyList<EvidenceDeviceGroup> Devices,
    int TotalCount);

/// <summary>
/// Groups evidence items by device within a day.
/// </summary>
public record EvidenceDeviceGroup(
    Guid DeviceId,
    string DeviceName,
    IReadOnlyList<EvidenceItem> Items);
