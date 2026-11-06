using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>A media player disposed while its module import is still pending must not attach its disposed bridge.</summary>
public class MediaPlayerDispose_Tests : BunitContext
{
    private const string MediaPlayerModule = "./_content/VideoForensics.Client.Ui.Shared/scripts/media-player.js";

    public MediaPlayerDispose_Tests()
    {
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
    }

    [Fact]
    public async Task MediaPlayer_DisposedWhileImportPending_DoesNotAttachTheDisposedBridge()
    {
        // Arrange: the module import stays pending until the test completes it
        var runtime = new PendingImportRuntime();
        _ = Services.AddSingleton<IJSRuntime>(runtime);
        var module = new Mock<IJSObjectReference>();

        IRenderedComponent<MediaPlayer> cut = Render<MediaPlayer>(p => p.Add(x => x.Source, "/media/clip/content"));

        // Act: the circuit disposes the player, then the import completes
        await ((IAsyncDisposable)cut.Instance).DisposeAsync();
        runtime.Import.SetResult(module.Object);
        await Task.Delay(100);

        // Assert: the disposed bridge is never handed to the module
        Assert.Equal(MediaPlayerModule, runtime.ImportedPath);
        Assert.DoesNotContain(module.Invocations, call => call.Arguments.Count > 0 && Equals(call.Arguments[0], "attach"));
    }

    /// <summary>Holds the module import open until the test completes it, so the player can be disposed in between.</summary>
    private sealed class PendingImportRuntime : IJSRuntime
    {
        public TaskCompletionSource<IJSObjectReference> Import { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? ImportedPath { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "import")
            {
                ImportedPath = args?[0] as string;
                return new ValueTask<TValue>((Task<TValue>)(object)Import.Task);
            }

            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }
}
