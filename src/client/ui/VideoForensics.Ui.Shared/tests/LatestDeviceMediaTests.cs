using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Devices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class LatestDeviceMediaTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static MediaItem Item(string format, DateTime recordedAt, string? metadata)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.Empty,
            FileName = "f." + format,
            FilePath = "p",
            MediaFormat = format,
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
        MediaItem older = Item("jpg", Now.AddMinutes(-30), Snapshot);
        MediaItem newest = Item("jpg", Now.AddMinutes(-1), Snapshot);
        MediaItem oldLoop = Item("mp4", Now.AddMinutes(-60), Loop);
        MediaItem newLoop = Item("mp4", Now.AddMinutes(-15), Loop);

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { older, oldLoop, newest, newLoop });

        Assert.Same(newest, selection.Snapshot);
        Assert.Same(newLoop, selection.Loop);
    }

    [Fact]
    public void Select_IgnoresEventVideosAndNonSnapshotJpegs()
    {
        MediaItem eventVideo = Item("mp4", Now, "{\"eventId\":\"e1\"}");
        MediaItem thumbnail = Item("jpg", Now, "{\"type\":\"event\"}");

        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(new[] { eventVideo, thumbnail });

        Assert.Null(selection.Snapshot);
        Assert.Null(selection.Loop);
    }

    [Fact]
    public void Select_NoMedia_ReturnsNothing()
    {
        LatestDeviceMedia.Selection selection = LatestDeviceMedia.Select(Array.Empty<MediaItem>());

        Assert.Null(selection.Snapshot);
        Assert.Null(selection.Loop);
    }
}
