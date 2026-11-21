

using VideoForensics.Core.Api.Contracts;
namespace VideoForensics.Client.Ui.Shared.Services.Devices;

/// <summary>
/// Picks the one snapshot and the one roll-up loop to show for a device on its Devices tab. Older snapshots and loops stay
/// in the database; the tab shows only the newest of each. Pure, so the selection rule is tested without a repository.
/// </summary>
public static class LatestDeviceMedia
{
    /// <summary>The newest snapshot JPEG and the newest roll-up loop; either is null when the device has none yet.</summary>
    public sealed record Selection(MediaItemDto? Snapshot, MediaItemDto? Loop);

    /// <summary>Selects from a device's recent media, in any order.</summary>
    public static Selection Select(IEnumerable<MediaItemDto> items)
    {
        MediaItemDto? snapshot = null;
        MediaItemDto? loop = null;
        foreach (MediaItemDto item in items)
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

    // Why: MediaKind is the typed classification (written by the snapshot capture and the roll-up). Text in MetadataJson is not read.
    private static bool IsSnapshot(MediaItemDto item)
    {
        return string.Equals(item.MediaKind, MediaKindNames.Snapshot, StringComparison.Ordinal)
        && string.Equals(item.MediaFormat, "jpg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoop(MediaItemDto item)
    {
        return string.Equals(item.MediaKind, MediaKindNames.Timelapse, StringComparison.Ordinal)
        && string.Equals(item.MediaFormat, "mp4", StringComparison.OrdinalIgnoreCase);
    }
}
