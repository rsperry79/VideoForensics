using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The camera grid: the first camera plays with controls, every other camera is a muted preview that opens the single view on click.</summary>
public class CameraGridU9_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public CameraGridU9_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
    }

    private static List<CameraTile> TwoTiles()
    {
        var clips = new List<ClipWindow> { new("clip-a", Start, 60) };
        return
        [
            new CameraTile("Front", clips, id => $"/media/{id}/content", id => $"/media/{id}/preview"),
            new CameraTile("Back", clips, id => $"/media/{id}/content", id => $"/media/{id}/preview"),
        ];
    }

    [Fact]
    public void CameraGrid_NonMasterTile_RendersAPreviewButtonAndNoFullPlayer()
    {
        // Arrange and Act
        IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
            .Add(x => x.Tiles, TwoTiles())
            .Add(x => x.Cursor, Start.AddSeconds(10)));

        // Assert: only the first tile is the full player, so one stream is full-content (U9)
        Assert.Single(cut.FindAll("[data-testid='camera-tile-preview']"));
        Assert.Single(cut.FindAll("[data-testid='camera-tile'] .review-stage"));
    }

    [Fact]
    public void CameraGrid_TileClick_RaisesOnSelectWithTheTileIndex()
    {
        // Arrange
        int? selected = null;
        IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
            .Add(x => x.Tiles, TwoTiles())
            .Add(x => x.Cursor, Start.AddSeconds(10))
            .Add(x => x.OnSelect, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<int>(this, i => selected = i)));

        // Act
        cut.Find("[data-testid='camera-tile-preview']").Click();

        // Assert
        Assert.Equal(1, selected);
    }
}
