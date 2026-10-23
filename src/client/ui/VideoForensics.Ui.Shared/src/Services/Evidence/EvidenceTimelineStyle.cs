namespace VideoForensics.Client.Ui.Shared.Services.Evidence;

/// <summary>
/// Display rules for a row on the Evidence timeline: the label text and the colour tone that marks what kind of item it is.
/// </summary>
public static class EvidenceTimelineStyle
{
    /// <summary>Tone for a media-only item, or an event whose type has no dedicated colour.</summary>
    public const string NoEventTone = "timeline-tone-none";

    /// <summary>Tone for a motion event.</summary>
    public const string MotionTone = "timeline-tone-motion";

    /// <summary>Tone for a snapshot still.</summary>
    public const string SnapshotTone = "timeline-tone-snapshot";

    /// <summary>Tone for a time lapse (a roll-up loop).</summary>
    public const string TimeLapseTone = "timeline-tone-timelapse";

    /// <summary>Tone for a doorbell ding.</summary>
    public const string DingTone = "timeline-tone-ding";

    /// <summary>Tone for a manually started event.</summary>
    public const string ManualTone = "timeline-tone-manual";

    /// <summary>Tone for any item pinned to a case. Takes precedence over the event-type tones.</summary>
    public const string InCaseTone = "timeline-tone-in-case";

    /// <summary>Label to show on the row: the item's label with its first letter capitalised.</summary>
    public static string DisplayLabel(EvidenceItem item)
    {
        return TitleCase(item.Label);
    }

    /// <summary>
    /// Returns the tone CSS class for a row. A case always wins, so a pinned item is red regardless of its event type.
    /// </summary>
    /// <param name="item">The evidence item shown on the row.</param>
    /// <param name="pinnedIds">IDs of events and media items pinned to any case.</param>
    public static string ToneClass(EvidenceItem item, IReadOnlySet<Guid> pinnedIds)
    {
        // Checked by ID rather than Key so an event row is red when its attached media is pinned, and vice versa.
        if ((item.Event is not null && pinnedIds.Contains(item.Event.Id)) ||
            (item.Media is not null && pinnedIds.Contains(item.Media.Id)))
        {
            return InCaseTone;
        }

        // Snapshots and time lapses are green; they have no event, so they are checked before the grey no-event tone.
        if (item.Kind == EvidenceKind.Snapshot)
        {
            return SnapshotTone;
        }

        if (item.IsTimeLapse)
        {
            return TimeLapseTone;
        }

        // Other media-only items have no event, so they stay grey like "No event".
        if (item.Event is null)
        {
            return NoEventTone;
        }

        // Provider event types arrive in lower case ("motion", "on_demand"); compare case-insensitively.
        return item.Event.EventType.ToLowerInvariant() switch
        {
            "motion" => MotionTone,
            "manual" or "on_demand" => ManualTone,
            "ding" => DingTone,
            _ => NoEventTone
        };
    }

    /// <summary>
    /// Words separated by underscores or spaces, each capitalised: "on_demand" reads "On Demand". Null or empty input is returned as-is.
    /// </summary>
    public static string TitleCase(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : string.Join(" ", value.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));
    }

    /// <summary>Upper-cases the first character and leaves the rest untouched. Null or empty input is returned as-is.</summary>
    public static string Capitalize(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
