using Microsoft.JSInterop;

namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>
/// The .NET end of the media-player.js event reports. It is a separate object so the component's own members stay
/// off the JavaScript-callable surface.
/// </summary>
internal sealed class MediaPlayerJsBridge(
    Func<double, double?, Task> onProgress,
    Func<double?, Task> onReady,
    Func<Task> onEnded,
    Func<string, Task> onError)
{
    [JSInvokable]
    public Task OnProgress(double positionSeconds, double? durationSeconds)
    {
        return onProgress(positionSeconds, durationSeconds);
    }

    [JSInvokable]
    public Task OnReady(double? durationSeconds)
    {
        return onReady(durationSeconds);
    }

    [JSInvokable]
    public Task OnEnded()
    {
        return onEnded();
    }

    [JSInvokable]
    public Task OnError(string code)
    {
        return onError(code);
    }
}
