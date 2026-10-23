namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Keeps one snapshot per device per roll-up window on the media view. Windows are aligned to the clock (the window
/// containing a time is that time divided by the window length), so every device's windows line up with the loops.
/// The newest snapshot in each window is kept. Events, videos and files pass through unchanged, and the original order
/// is preserved so the view's default selection does not move.
/// </summary>
public static class SnapshotWindowThinning
{
    /// <summary>
    /// Returns the items with at most one snapshot per device per window, in their original order. <paramref name="windowFor"/>
    /// gives each device's window length (its account's roll-up interval).
    /// </summary>
    public static IReadOnlyList<EvidenceItem> Apply(IEnumerable<EvidenceItem> items, Func<Guid, TimeSpan> windowFor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(windowFor);

        var list = items.ToList();
        var newestPerWindow = new Dictionary<(Guid Device, long Bucket), EvidenceItem>();
        foreach (EvidenceItem item in list)
        {
            if (!IsThinnable(item, windowFor, out TimeSpan window))
            {
                continue;
            }

            long bucket = item.OccurredAtUtc.Ticks / window.Ticks;
            var key = (item.DeviceId, bucket);
            if (!newestPerWindow.TryGetValue(key, out EvidenceItem? current) || item.OccurredAtUtc > current.OccurredAtUtc)
            {
                newestPerWindow[key] = item;
            }
        }

        var keep = new HashSet<EvidenceItem>(newestPerWindow.Values, ReferenceEqualityComparer.Instance);
        return list
            .Where(item => !IsThinnable(item, windowFor, out _) || keep.Contains(item))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>True for a snapshot with a usable window; other items and zero-length windows are never thinned.</summary>
    private static bool IsThinnable(EvidenceItem item, Func<Guid, TimeSpan> windowFor, out TimeSpan window)
    {
        window = item.Kind == EvidenceKind.Snapshot ? windowFor(item.DeviceId) : TimeSpan.Zero;
        return item.Kind == EvidenceKind.Snapshot && window > TimeSpan.Zero;
    }
}
