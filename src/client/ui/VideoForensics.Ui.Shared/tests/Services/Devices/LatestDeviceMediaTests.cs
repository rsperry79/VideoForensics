using VideoForensics.Client.Ui.Shared.Services.Devices;using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Devices;

public class LatestDeviceMediaTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static MediaItem Item(string format, DateTime recordedAt, string? metadata, string kind = MediaKinds.Clip)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.Empty,
            FileName = "f." + format,
            FilePath = "p",
            MediaFormat = format,
            MediaKind = kind,
            RecordedAtUtc = recordedAt,
            Sha256Hash = "h",
            MetadataJson = metadata,
        };
    }

    private const string Snapshot = "{\"eventId\":\"snapshot-1\",\"type\":\"snapshot\"}";
    private const string Loop = "{\"type\":\"snapshot-rollup\"}";

    [Fact]
    public void Select_ReturnsOnlyTheNewestSnapshotAndNewestLoop()
    {
        MediaItem older = Item("jpg", Now.AddMinutes(-30), Snapshot, MediaKinds.Snapshot);
        MediaItem newest = Item("jpg", Now.AddMinutes(-1), Snapshot, MediaKinds.Snapshot);
        MediaItem oldLoop = Item("mp4", Now.AddMinutes(-60), Loop, MediaKinds.Timelapse);
        MediaItem newLoop = Item("mp4", Now.AddMinutes(-15), Loop, MediaKinds.Timelapse);

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { older.ToDto(), oldLoop.ToDto(), newest.ToDto(), newLoop.ToDto() });

        Assert.Equal(newest.ToDto(), selection.Snapshot);
        Assert.Equal(newLoop.ToDto(), selection.Loop);
    }

    [Fact]
    public void Select_IgnoresEventVideosAndNonSnapshotJpegs()
    {
        MediaItem eventVideo = Item("mp4", Now, "{\"eventId\":\"e1\"}");
        MediaItem thumbnail = Item("jpg", Now, "{\"type\":\"event\"}");

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { eventVideo.ToDto(), thumbnail.ToDto() });

        Assert.Null(selection.Snapshot);
        Assert.Null(selection.Loop);
    }

    [Fact]
    public void Select_ClassifiesByMediaKind_WithoutAMetadataMarker()
    {
        // Why: the typed MediaKind column is the classification. A row with no JSON marker is still a snapshot or a loop.
        MediaItem snapshot = Item("jpg", Now, null, MediaKinds.Snapshot);
        MediaItem loop = Item("mp4", Now, null, MediaKinds.Timelapse);

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { snapshot.ToDto(), loop.ToDto() });

        Assert.Equal(snapshot.ToDto(), selection.Snapshot);
        Assert.Equal(loop.ToDto(), selection.Loop);
    }

    [Fact]
    public void Select_MarkerTextOnAClipRow_IsNotASnapshotOrLoop()
    {
        // Why: text that happens to contain a marker must not reclassify a row whose MediaKind says Clip.
        MediaItem clip = Item("jpg", Now, Snapshot, MediaKinds.Clip);
        MediaItem clipLoop = Item("mp4", Now, Loop, MediaKinds.Clip);

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { clip.ToDto(), clipLoop.ToDto() });

        Assert.Null(selection.Snapshot);
        Assert.Null(selection.Loop);
    }

    [Fact]
    public void Select_NoMedia_ReturnsNothing()
    {
        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(Array.Empty<MediaItemDto>());

        Assert.Null(selection.Snapshot);
        Assert.Null(selection.Loop);
    }
}
