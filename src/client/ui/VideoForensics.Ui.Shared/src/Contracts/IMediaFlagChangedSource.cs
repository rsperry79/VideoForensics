using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "media flag changed" events, so an open flag list or review page reloads without an operator
    /// pressing Refresh. The WebApp receives it in-process from its endpoints; remote hosts (MAUI) forward the server's realtime push.
    /// Events are not replayed: a new subscriber hears only later changes.
    /// </summary>
    public interface IMediaFlagChangedSource
    {
        /// <summary>Stream of media flag changes. Subscribers must dispose their subscription.</summary>
        IObservable<MediaFlagChangedDto> MediaFlagChanged { get; }
    }
}
