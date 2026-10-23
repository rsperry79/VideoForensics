using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Devices;

/// <summary>
/// Picks the one snapshot and the one roll-up loop to show for a device on its Devices tab. Older snapshots and loops stay
/// in the database; the tab shows only the newest of each. Pure, so the selection rule is tested without a repository.
/// </summary>
public static class LatestDeviceMedia
{
    /// <summary>Marker the snapshot download writes into a snapshot's metadata.</summary>
    private const string SnapshotMarker = "\"type\":\"snapshot\"";

    /// <summary>Marker the roll-up writes into a loop's metadata.</summary>
    private const string LoopMarker = "snapshot-rollup";

    /// <summary>The newest snapshot JPEG and the newest roll-up loop; either is null when the device has none yet.</summary>
    public sealed record Selection(MediaItem? Snapshot, MediaItem? Loop);

    /// <summary>Selects from a device's recent media, in any order.</summary>
    public static Selection Select(IEnumerable<MediaItem> items)
    {
        MediaItem? snapshot = null;
        MediaItem? loop = null;
        foreach (MediaItem item in items)
        {
            if (IsSnapshot(item) && (snapshot is null || item.RecordedAtUtc > snapshot.RecordedAtUtc))
            {
                snapshot = item;
            }

            if (IsLoop(item) && (loop is null || item.RecordedAtUtc > loop.RecordedAtUtc))
            {
                loop = item;
            }
        }

        return new Selection(snapshot, loop);
    }

    private static bool IsSnapshot(MediaItem item)
    {
        return string.Equals(item.MediaFormat, "jpg", StringComparison.OrdinalIgnoreCase)
        && item.MetadataJson is not null
        && item.MetadataJson.Contains(SnapshotMarker, StringComparison.Ordinal);
    }

    private static bool IsLoop(MediaItem item)
    {
        return string.Equals(item.MediaFormat, "mp4", StringComparison.OrdinalIgnoreCase)
        && item.MetadataJson is not null
        && item.MetadataJson.Contains(LoopMarker, StringComparison.Ordinal);
    }
}
