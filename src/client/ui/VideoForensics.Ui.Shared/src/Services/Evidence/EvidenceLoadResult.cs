using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Result of loading evidence items for a forensic scope.
/// </summary>
public class EvidenceLoadResult
{
    /// <summary>
    /// All evidence items (events and media-only) for the scope, ordered by OccurredAtUtc descending.
    /// </summary>
    public required IReadOnlyList<EvidenceItem> Items { get; init; }

    /// <summary>
    /// Active legal holds indexed by media item ID.
    /// </summary>
    public required IReadOnlyDictionary<Guid, LegalHold> ActiveHoldsByMediaItemId { get; init; }

    /// <summary>
    /// Integrity records from the event load result.
    /// </summary>
    public required IReadOnlyList<IntegrityRecord> IntegrityRecords { get; init; }

    /// <summary>
    /// Per-device errors captured during loading (device ID → error message).
    /// </summary>
    public required IReadOnlyDictionary<Guid, string> Errors { get; init; }

    /// <summary>
    /// Count of items by kind.
    /// </summary>
    public EvidenceItemCounts Counts
    {
        get
        {
            var eventCount = Items.Count(i => i.Kind == EvidenceKind.Event);
            var snapshotCount = Items.Count(i => i.Kind == EvidenceKind.Snapshot);
            var videoCount = Items.Count(i => i.Kind == EvidenceKind.Video);
            var fileCount = Items.Count(i => i.Kind == EvidenceKind.File);
            return new EvidenceItemCounts(eventCount, snapshotCount, videoCount, fileCount);
        }
    }
}

/// <summary>
/// Counts of evidence items by kind.
/// </summary>
public record EvidenceItemCounts(int Events, int Snapshots, int Videos, int Files)
{
    /// <summary>
    /// Total count of all items.
    /// </summary>
    public int Total => Events + Snapshots + Videos + Files;
}
