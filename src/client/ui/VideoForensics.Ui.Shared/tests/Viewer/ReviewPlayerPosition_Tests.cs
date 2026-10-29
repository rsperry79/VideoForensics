using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>The review player's playback position read, and the single-camera view that forwards it to its main player.</summary>
public class ReviewPlayerPosition_Tests : BunitContext
{
    private const string MediaViewerModule = "./_content/VideoForensics.Client.Ui.Shared/scripts/media-viewer.js";
    private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public ReviewPlayerPosition_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
    }

    private static List<ClipWindow> OneClip() => [new("clip-a", Start, 60)];

    /// <summary>Sets up the media-viewer module so the position read returns <paramref name="ms"/>.</summary>
    private Bunit.BunitJSModuleInterop SetupModuleReturning(double ms)
    {
        Bunit.BunitJSModuleInterop module = JSInterop.SetupModule(MediaViewerModule);
        _ = module.Setup<double>("getCurrentTimeMs", _ => true).SetResult(ms);
        return module;
    }

    [Fact]
    public async Task ReviewPlayer_PlaybackPosition_ReturnsTheVideoCurrentTimeInWholeMilliseconds()
    {
        // Arrange
        _ = SetupModuleReturning(4200.4);
        IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
            .Add(x => x.Clips, OneClip())
            .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
            .Add(x => x.Cursor, Start.AddSeconds(10)));

        // Act
        long? position = await cut.Instance.GetCurrentTimeMsAsync();

        // Assert
        Assert.Equal(4200L, position);
    }

    [Fact]
    public async Task ReviewPlayer_NoVideoRendered_ReturnsNullWithoutCallingTheVideo()
    {
        // Arrange: the cursor is in a gap, so no video element is rendered
        _ = SetupModuleReturning(1000.0);
        IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
            .Add(x => x.Clips, OneClip())
            .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
            .Add(x => x.Cursor, Start.AddMinutes(10)));

        // Act
        long? position = await cut.Instance.GetCurrentTimeMsAsync();

        // Assert
        Assert.Null(position);
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier == "getCurrentTimeMs");
    }

    [Fact]
    public async Task ReviewPlayer_PositionCallThrows_ReturnsNull()
    {
        // Arrange
        Bunit.BunitJSModuleInterop module = JSInterop.SetupModule(MediaViewerModule);
        _ = module.Setup<double>("getCurrentTimeMs", _ => true).SetException(new JSException("boom"));
        IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
            .Add(x => x.Clips, OneClip())
            .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
            .Add(x => x.Cursor, Start.AddSeconds(10)));

        // Act
        long? position = await cut.Instance.GetCurrentTimeMsAsync();

        // Assert
        Assert.Null(position);
    }

    [Fact]
    public async Task SingleCameraView_PlaybackPosition_ForwardsToTheMainPlayer()
    {
        // Arrange
        _ = SetupModuleReturning(1500.0);
        var cameras = new List<CameraTile>
        {
            new("Front", OneClip(), id => $"/media/{id}/content", id => $"/media/{id}/preview"),
            new("Back", OneClip(), id => $"/media/{id}/content", id => $"/media/{id}/preview"),
        };
        IRenderedComponent<SingleCameraView> cut = Render<SingleCameraView>(p => p
            .Add(x => x.Cameras, cameras)
            .Add(x => x.SelectedIndex, 0)
            .Add(x => x.Cursor, Start.AddSeconds(10)));

        // Act
        long? position = await cut.Instance.GetCurrentTimeMsAsync();

        // Assert
        Assert.Equal(1500L, position);
    }

    [Fact]
    public async Task SingleCameraView_NoMainPlayer_ReturnsNull()
    {
        // Arrange
        IRenderedComponent<SingleCameraView> cut = Render<SingleCameraView>(p => p
            .Add(x => x.Cameras, new List<CameraTile>())
            .Add(x => x.SelectedIndex, 0));

        // Act
        long? position = await cut.Instance.GetCurrentTimeMsAsync();

        // Assert
        Assert.Null(position);
    }
}
