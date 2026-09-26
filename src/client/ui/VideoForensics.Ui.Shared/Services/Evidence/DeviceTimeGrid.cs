using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// One time bucket for one device row in the device-by-time grid.
/// </summary>
/// <param name="StartUtc">Inclusive start of the bucket, in UTC.</param>
/// <param name="EndUtc">Exclusive end of the bucket, in UTC.</param>
/// <param name="EventCount">Number of <see cref="EvidenceKind.Event"/> items in this bucket.</param>
/// <param name="SnapshotCount">Number of media items (snapshot or video) in this bucket.</param>
/// <param name="IsGap">
/// True if this bucket has no activity, but the same device has activity in some earlier bucket
/// and some later bucket within the grid - i.e. a run of silence surrounded by activity, which is
/// what makes it interesting for jamming/anomaly review rather than merely "before it started" or
/// "after it stopped".
/// </param>
public sealed record DeviceTimeBucket(
    DateTime StartUtc,
    DateTime EndUtc,
    int EventCount,
    int SnapshotCount,
    bool IsGap)
{
    /// <summary>True if this bucket has no events and no media markers at all.</summary>
    public bool IsEmpty => EventCount == 0 && SnapshotCount == 0;
}

/// <summary>One device's row of buckets in the device-by-time grid.</summary>
public sealed record DeviceTimeRow(
    Guid DeviceId,
    string DeviceName,
    IReadOnlyList<DeviceTimeBucket> Buckets);

/// <summary>The full device-by-time grid: shared column headers plus one row per device.</summary>
public sealed record DeviceTimeGridResult(
    IReadOnlyList<DateTime> BucketStartsUtc,
    TimeSpan BucketSize,
    IReadOnlyList<DeviceTimeRow> Rows);

/// <summary>
/// Raised by <c>DeviceTimeGridView</c> when a cell is clicked - identifies the device and bucket
/// range so the caller can narrow <c>ScopeState</c> to it and switch to the Timeline view.
/// </summary>
public sealed record DeviceTimeCellClickArgs(Guid DeviceId, DateTime BucketStartUtc, DateTime BucketEndUtc);

/// <summary>
/// Builds the device x hour grid used by the Evidence "Device x Time" view for gap-spotting
/// (jamming/anomaly review): rows are devices, columns are time buckets across the scope's date
/// range, and cells carry event/snapshot counts.
/// </summary>
public static class DeviceTimeGrid
{
    /// <summary>
    /// The maximum number of columns the grid will render. A plain 7-day range at hourly
    /// resolution is exactly this many columns (7 * 24); wider ranges switch to a coarser,
    /// multi-hour bucket so the column count never exceeds this cap.
    /// </summary>
    public const int MaxColumns = 168;

    /// <summary>
    /// Builds the grid for the given time range, devices and evidence items. Bucket size is one
    /// hour for ranges up to <see cref="MaxColumns"/> hours; wider ranges switch to the smallest
    /// whole number of hours per bucket that still keeps the column count at or under the cap, so
    /// the grid stays readable regardless of how wide the scope's date range is.
    /// </summary>
    /// <param name="fromUtc">Inclusive start of the range, in UTC.</param>
    /// <param name="toUtc">Exclusive end of the range, in UTC. Must be after <paramref name="fromUtc"/>.</param>
    /// <param name="devices">The devices to render as rows.</param>
    /// <param name="items">Evidence items (events and media) to bucket; items for devices not in <paramref name="devices"/> are ignored.</param>
    public static DeviceTimeGridResult Build(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<Device> devices,
        IReadOnlyList<EvidenceItem> items)
    {
        if (toUtc <= fromUtc)
        {
            return new DeviceTimeGridResult(Array.Empty<DateTime>(), TimeSpan.FromHours(1), Array.Empty<DeviceTimeRow>());
        }

        var totalHours = (toUtc - fromUtc).TotalHours;
        var bucketHours = totalHours <= MaxColumns ? 1 : (int)Math.Ceiling(totalHours / MaxColumns);
        var bucketSize = TimeSpan.FromHours(bucketHours);

        var bucketStarts = new List<DateTime>();
        for (var start = fromUtc; start < toUtc; start += bucketSize)
        {
            bucketStarts.Add(start);
        }

        var itemsByDevice = items
            .GroupBy(i => i.DeviceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<DeviceTimeRow>(devices.Count);
        foreach (var device in devices)
        {
            var deviceItems = itemsByDevice.TryGetValue(device.Id, out var list) ? list : new List<EvidenceItem>();

            var buckets = new List<DeviceTimeBucket>(bucketStarts.Count);
            foreach (var bucketStart in bucketStarts)
            {
                var bucketEnd = bucketStart + bucketSize;
                var eventCount = 0;
                var snapshotCount = 0;
                foreach (var item in deviceItems)
                {
                    if (item.OccurredAtUtc < bucketStart || item.OccurredAtUtc >= bucketEnd)
                    {
                        continue;
                    }

                    if (item.Kind == EvidenceKind.Event)
                    {
                        eventCount++;
                    }
                    else
                    {
                        // Snapshot, Video, and File all render as a media marker in the cell.
                        snapshotCount++;
                    }
                }

                buckets.Add(new DeviceTimeBucket(bucketStart, bucketEnd, eventCount, snapshotCount, IsGap: false));
            }

            rows.Add(new DeviceTimeRow(device.Id, device.Name, MarkGaps(buckets)));
        }

        return new DeviceTimeGridResult(bucketStarts, bucketSize, rows);
    }

    /// <summary>
    /// A gap is a run of empty buckets strictly between the device's first and last bucket with
    /// any activity - a leading or trailing run of silence isn't a gap (the device simply hadn't
    /// started, or had already stopped, within the scope's range), but silence sandwiched between
    /// two active buckets is exactly the anomaly this view exists to surface.
    /// </summary>
    private static List<DeviceTimeBucket> MarkGaps(List<DeviceTimeBucket> buckets)
    {
        var firstActivity = buckets.FindIndex(b => !b.IsEmpty);
        if (firstActivity < 0)
        {
            // No activity anywhere in range: nothing to call a gap.
            return buckets;
        }

        var lastActivity = buckets.FindLastIndex(b => !b.IsEmpty);

        var result = new List<DeviceTimeBucket>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            var isGap = bucket.IsEmpty && i > firstActivity && i < lastActivity;
            result.Add(bucket with { IsGap = isGap });
        }

        return result;
    }
}
