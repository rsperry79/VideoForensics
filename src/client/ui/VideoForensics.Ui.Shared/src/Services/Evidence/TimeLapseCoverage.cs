namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

/// <summary>
/// Hides the snapshots that a Time Lapse stands in for, in the media view only. A Time Lapse covers the window that ends at
/// its recorded time and is one roll-up interval long; the snapshots of that device inside that window are its placeholders.
/// Nothing is deleted: the files and their records stay, and only the view leaves them out.
/// </summary>
public static class TimeLapseCoverage
{
    /// <summary>Returns the items without the snapshots that a Time Lapse covers. Other items keep their order.</summary>
    public static IReadOnlyList<EvidenceItem> HideCovered(IEnumerable<EvidenceItem> items, Func<Guid, TimeSpan> windowFor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(windowFor);

        var list = items.ToList();
        var lapses = list.Where(i => i.IsTimeLapse && i.Media is not null).ToList();
        if (lapses.Count == 0)
        {
            return list.AsReadOnly();
        }

        bool Covered(EvidenceItem item)
        {
            return item.Kind == EvidenceKind.Snapshot && lapses.Any(lapse =>
        {
            TimeSpan window = windowFor(lapse.DeviceId);
            DateTime end = lapse.Media!.RecordedAtUtc;
            DateTime start = end - window;
            return lapse.DeviceId == item.DeviceId && item.OccurredAtUtc >= start && item.OccurredAtUtc < end;
        });
        }

        return list.Where(item => !Covered(item)).ToList().AsReadOnly();
    }
}
