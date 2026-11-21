using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Viewer;

public class MediaPlayerDisposeTests : BunitContext
{
    private const string ModulePath = "./_content/VideoForensics.Client.Ui.Shared/scripts/media-player.js";

    public MediaPlayerDisposeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
    }

    [Fact]
    public async Task MediaPlayer_DisposeWhenTheCircuitCancelsDetach_DoesNotThrow()
    {
        // Disposing after the circuit has dropped cancels the pending JS call. That must not surface as an
        // unhandled circuit exception, which would end the session.
        Bunit.BunitJSModuleInterop module = JSInterop.SetupModule(ModulePath);
        _ = module.SetupVoid("detach", _ => true).SetException(new TaskCanceledException());

        IRenderedComponent<MediaPlayer> cut = Render<MediaPlayer>(p => p.Add(x => x.Source, "/clip.mp4"));

        IAsyncDisposable disposable = cut.Instance;
        Exception? exception = await Record.ExceptionAsync(() => disposable.DisposeAsync().AsTask());
        Assert.Null(exception);
    }
}
