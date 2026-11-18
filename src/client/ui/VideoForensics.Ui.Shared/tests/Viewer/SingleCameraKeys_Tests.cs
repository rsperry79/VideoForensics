using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The player keyboard map on the single-camera stage: Space plays or pauses, S grabs a still, other keys are left alone.</summary>
public class SingleCameraKeys_Tests : BunitContext
{
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public SingleCameraKeys_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
    }

    private IRenderedComponent<SingleCameraView> RenderView(EventCallback onGrabStill = default)
    {
        var clips = new List<ClipWindow> { new("clip-a", Start, 60) };
        var tiles = new List<CameraTile> { new("Front", clips, id => $"/media/{id}/content") };
        return Render<SingleCameraView>(p => p
            .Add(x => x.Cameras, tiles)
            .Add(x => x.SelectedIndex, 0)
            .Add(x => x.Cursor, Start.AddSeconds(10))
            .Add(x => x.OnGrabStill, onGrabStill));
    }

    [Fact]
    public void SingleCameraView_SpaceKey_TogglesPlayback()
    {
        // Arrange
        IRenderedComponent<SingleCameraView> cut = RenderView();

        // Act
        cut.Find("[data-testid='single-camera-stage']").KeyDown(new KeyboardEventArgs { Key = " " });

        // Assert
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "togglePlay");
    }

    [Fact]
    public void SingleCameraView_SKey_RaisesOnGrabStill()
    {
        // Arrange
        bool grabbed = false;
        IRenderedComponent<SingleCameraView> cut = RenderView(EventCallback.Factory.Create(this, () => grabbed = true));

        // Act
        cut.Find("[data-testid='single-camera-stage']").KeyDown(new KeyboardEventArgs { Key = "s" });

        // Assert
        Assert.True(grabbed);
    }

    [Fact]
    public void SingleCameraView_TabKey_IsLeftAlone()
    {
        // Arrange
        bool grabbed = false;
        IRenderedComponent<SingleCameraView> cut = RenderView(EventCallback.Factory.Create(this, () => grabbed = true));

        // Act
        cut.Find("[data-testid='single-camera-stage']").KeyDown(new KeyboardEventArgs { Key = "Tab" });

        // Assert: Tab must still move focus, so the stage never swallows it
        Assert.False(grabbed);
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "togglePlay");
    }
}
