namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

/// <summary>
/// Hides the snapshots that a Time Lapse stands in for, in the media view only. A Time Lapse covers its own span, from its
/// recorded start for its duration; the snapshots of that device inside that span are its placeholders.
/// Nothing is deleted: the files and their records stay, and only the view leaves them out.
/// </summary>
public static class TimeLapseCoverage
{
    /// <summary>Returns the items without the snapshots that a Time Lapse covers. Other items keep their order.</summary>
    public static IReadOnlyList<EvidenceItem> HideCovered(IEnumerable<EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var list = items.ToList();
        var lapses = list.Where(i => i.IsTimeLapse && i.Media is not null).ToList();
        if (lapses.Count == 0)
        {
            return list.AsReadOnly();
        }

        bool Covered(EvidenceItem item)
        {
            // A time lapse's span is its start plus its own duration. Variable-length spans need no fixed window.
            return item.Kind == EvidenceKind.Snapshot && lapses.Any(lapse =>
        {
            DateTime start = lapse.Media!.RecordedAtUtc;
            DateTime end = start.AddSeconds(lapse.Media.DurationSeconds ?? 0);
            return lapse.DeviceId == item.DeviceId && item.OccurredAtUtc >= start && item.OccurredAtUtc < end;
        });
        }

        return list.Where(item => !Covered(item)).ToList().AsReadOnly();
    }
}
