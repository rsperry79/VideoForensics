namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Keeps one snapshot per device per roll-up window on the media view. Windows are aligned to the clock (the window
/// containing a time is that time divided by the window length), so every device's windows line up with the loops.
/// The newest snapshot in each window is kept. Events, videos and files pass through unchanged.
/// </summary>
public static class SnapshotWindowThinning
{
    /// <summary>
    /// Returns the items with at most one snapshot per device per window. <paramref name="windowFor"/> gives each
    /// device's window length (its account's roll-up interval).
    /// </summary>
    public static IReadOnlyList<EvidenceItem> Apply(IEnumerable<EvidenceItem> items, Func<Guid, TimeSpan> windowFor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(windowFor);

        var newestPerWindow = new Dictionary<(Guid Device, long Bucket), EvidenceItem>();
        var kept = new List<EvidenceItem>();
        foreach (EvidenceItem item in items)
        {
            if (item.Kind != EvidenceKind.Snapshot)
            {
                kept.Add(item);
                continue;
            }

            TimeSpan window = windowFor(item.DeviceId);
            if (window <= TimeSpan.Zero)
            {
                kept.Add(item);
                continue;
            }

            long bucket = item.OccurredAtUtc.Ticks / window.Ticks;
            var key = (item.DeviceId, bucket);
            if (!newestPerWindow.TryGetValue(key, out EvidenceItem? current) || item.OccurredAtUtc > current.OccurredAtUtc)
            {
                newestPerWindow[key] = item;
            }
        }

        kept.AddRange(newestPerWindow.Values);
        return kept.OrderBy(i => i.OccurredAtUtc).ToList().AsReadOnly();
    }
}
