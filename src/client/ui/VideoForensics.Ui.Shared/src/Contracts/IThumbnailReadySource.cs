using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "a thumbnail was just produced" events, so an open evidence view can show a new still
    /// without a reload. Local hosts (WebApp) receive it in-process from the thumbnail generator; remote hosts (MAUI)
    /// forward the server's realtime push. Events are not replayed: a new subscriber hears only later events.
    /// </summary>
    public interface IThumbnailReadySource
    {
        /// <summary>Stream of produced thumbnails. Subscribers must dispose their subscription.</summary>
        IObservable<ThumbnailReadyDto> ThumbnailReady { get; }
    }
}
