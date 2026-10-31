using System.Text.Json;
using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Turns media items into the clips and camera tiles the review page draws. Pure functions, so the rules
/// (which items play, what covers the cursor) are tested without a page.
/// </summary>
public static class ReviewClipBuilder
{
    // The host writes these markers (SnapshotRollupService, SnapshotCaptureService). Ui.Shared cannot reference the
    // host, so the values are repeated here and must change together.
    private const string RollupType = "snapshot-rollup";
    private const string SnapshotType = "snapshot";
    private const string CaptureSource = "capture";

    /// <summary>
    /// The playable clips of one device, sorted by start. Excludes purged items, non-video items, time-lapses, timer stills,
    /// and items with no positive duration (a clip with no length can never cover a time on the timeline).
    /// </summary>
    public static IReadOnlyList<ClipWindow> BuildClips(IEnumerable<MediaItem> items, Guid deviceId)
    {
        return items
            .Where(item => item.DeviceId == deviceId && IsPlayableClip(item))
            .OrderBy(item => item.RecordedAtUtc)
            .Select(item => new ClipWindow(item.Id.ToString("D"), item.RecordedAtUtc, item.DurationSeconds!.Value))
            .ToList();
    }

    /// <summary>
    /// One tile per requested camera, in the requested order. Each tile holds ALL of that camera's playable clips, oldest
    /// first: the player resolves the clip covering the cursor itself and must be able to continue into the next clip, so
    /// a tile with no clips simply stays on the grid (for example during a gap).
    /// </summary>
    /// <param name="items">Media items for all requested cameras.</param>
    /// <param name="cameraIds">The cameras to show, in display order.</param>
    /// <param name="deviceNames">Display names by device id. A camera with no name is labelled with its id.</param>
    /// <param name="contentUrl">Video URL for an item.</param>
    /// <param name="previewUrl">Short preview URL for an item.</param>
    public static IReadOnlyList<CameraTile> BuildTiles(
        IEnumerable<MediaItem> items,
        IReadOnlyList<Guid> cameraIds,
        IReadOnlyDictionary<Guid, string> deviceNames,
        Func<MediaItem, string?> contentUrl,
        Func<MediaItem, string?> previewUrl)
    {
        List<MediaItem> all = items.ToList();
        var byClipId = new Dictionary<string, MediaItem>(StringComparer.Ordinal);
        foreach (MediaItem item in all)
        {
            byClipId[item.Id.ToString("D")] = item;
        }

        var tiles = new List<CameraTile>(cameraIds.Count);
        foreach (Guid cameraId in cameraIds)
        {
            IReadOnlyList<ClipWindow> clips = BuildClips(all, cameraId);

            string label = deviceNames.TryGetValue(cameraId, out string? name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : cameraId.ToString("D");

            tiles.Add(new CameraTile(
                label,
                clips,
                id => byClipId.TryGetValue(id, out MediaItem? item) ? contentUrl(item) : null,
                id => byClipId.TryGetValue(id, out MediaItem? item) ? previewUrl(item) : null));
        }

        return tiles;
    }

    private static bool IsPlayableClip(MediaItem item)
    {
        return !item.IsPurged
            && MediaFormatHelper.IsVideo(item.MediaFormat)
            && item.DurationSeconds is double duration && duration > 0
            && !IsTimeLapseOrTimerStill(item.MetadataJson);
    }

    /// <summary>
    /// True for a roll-up loop or a timer still. Malformed or missing metadata is not a marker, so the item stays playable.
    /// </summary>
    private static bool IsTimeLapseOrTimerStill(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(metadataJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? type = ReadString(root, "type");
            string? source = ReadString(root, "source");
            return string.Equals(type, RollupType, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(type, SnapshotType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(source, CaptureSource, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
