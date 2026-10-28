using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

public class ReviewClipBuilder_Tests
{
    private static readonly DateTime Start = new(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);

    private static MediaItem Item(
        Guid device,
        DateTime startUtc,
        double? duration,
        string format = "video/mp4",
        string? metadata = null,
        bool purged = false)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = device,
            FileName = "clip.mp4",
            FilePath = "clips/clip.mp4",
            MediaFormat = format,
            Sha256Hash = "hash",
            RecordedAtUtc = startUtc,
            DurationSeconds = duration,
            MetadataJson = metadata,
            IsPurged = purged,
        };
    }

    [Fact]
    public void BuildClips_TimeLapse_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 60, metadata: "{\"type\":\"snapshot-rollup\"}") };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_TimeLapseMarkerDifferentCase_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 60, metadata: "{\"type\":\"Snapshot-Rollup\"}") };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_TimerStill_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 1, metadata: "{\"type\":\"snapshot\",\"source\":\"capture\"}") };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_Purged_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 60, purged: true) };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_NonVideo_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 60, format: "image/jpeg") };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_OtherDevice_Excluded()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(Guid.NewGuid(), Start, 60) };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_UnsortedItems_SortedByStart()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        MediaItem late = Item(device, Start.AddMinutes(5), 30);
        MediaItem early = Item(device, Start, 30);
        var items = new[] { late, early };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Equal([early.Id.ToString("D"), late.Id.ToString("D")], clips.Select(c => c.Id));
        Assert.Equal(Start, clips[0].StartUtc);
        Assert.Equal(30, clips[0].DurationSeconds);
    }

    [Fact]
    public void BuildClips_NoOrZeroDuration_Skipped()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, null), Item(device, Start, 0) };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Empty(clips);
    }

    [Fact]
    public void BuildClips_NullOrMalformedMetadata_Included()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var items = new[] { Item(device, Start, 60, metadata: null), Item(device, Start.AddMinutes(1), 60, metadata: "{not json") };

        // Act
        IReadOnlyList<ClipWindow> clips = ReviewClipBuilder.BuildClips(items, device);

        // Assert
        Assert.Equal(2, clips.Count);
    }

    [Fact]
    public void BuildTiles_OneTilePerCameraInRequestedOrder()
    {
        // Arrange
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [a] = "Front", [b] = "Back" };

        // Act
        IReadOnlyList<CameraTile> tiles = ReviewClipBuilder.BuildTiles(
            [], [b, a], names, _ => null, _ => null);

        // Assert
        Assert.Equal(["Back", "Front"], tiles.Select(t => t.Label));
    }

    [Fact]
    public void BuildTiles_Camera_TileHoldsAllItsClipsOldestFirst()
    {
        // Arrange: the player picks the clip covering the cursor from the full list, so the tile must carry every clip.
        Guid camera = Guid.NewGuid();
        MediaItem later = Item(camera, Start.AddMinutes(10), 60);
        MediaItem earlier = Item(camera, Start, 60);
        MediaItem otherCamera = Item(Guid.NewGuid(), Start, 60);

        // Act
        IReadOnlyList<CameraTile> tiles = ReviewClipBuilder.BuildTiles(
            [later, earlier, otherCamera], [camera], new Dictionary<Guid, string>(), _ => null, _ => null);

        // Assert
        Assert.Single(tiles);
        Assert.Equal([earlier.Id.ToString("D"), later.Id.ToString("D")], tiles[0].Clips.Select(c => c.Id));
    }

    [Fact]
    public void BuildTiles_CameraWithNoClips_TileKeptEmpty()
    {
        // Arrange
        Guid camera = Guid.NewGuid();

        // Act
        IReadOnlyList<CameraTile> tiles = ReviewClipBuilder.BuildTiles(
            [], [camera], new Dictionary<Guid, string>(), _ => null, _ => null);

        // Assert
        Assert.Single(tiles);
        Assert.Empty(tiles[0].Clips);
    }

    [Fact]
    public void BuildTiles_UnnamedCamera_LabelIsDeviceId()
    {
        // Arrange
        Guid camera = Guid.NewGuid();

        // Act
        IReadOnlyList<CameraTile> tiles = ReviewClipBuilder.BuildTiles(
            [], [camera], new Dictionary<Guid, string>(), _ => null, _ => null);

        // Assert
        Assert.Equal(camera.ToString("D"), tiles[0].Label);
    }

    [Fact]
    public void BuildTiles_ContentAndPreviewUrls_ResolvedByClipId()
    {
        // Arrange
        Guid camera = Guid.NewGuid();
        MediaItem clip = Item(camera, Start, 60);
        string id = clip.Id.ToString("D");

        // Act
        IReadOnlyList<CameraTile> tiles = ReviewClipBuilder.BuildTiles(
            [clip],
            [camera],
            new Dictionary<Guid, string>(),
            m => "content/" + m.Id.ToString("D"),
            m => "preview/" + m.Id.ToString("D"));

        // Assert
        Assert.Equal("content/" + id, tiles[0].ContentUrlFor(id));
        Func<string, string?>? preview = tiles[0].PreviewUrlFor;
        Assert.NotNull(preview);
        Assert.Equal("preview/" + id, preview(id));
        Assert.Null(tiles[0].ContentUrlFor("unknown"));
    }
}
