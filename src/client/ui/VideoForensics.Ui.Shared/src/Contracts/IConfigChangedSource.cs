using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "settings changed" events, so an open settings view shows new values without a reload.
    /// The WebApp receives it in-process from the saving writer; remote hosts (MAUI) forward the server's realtime push.
    /// Events are not replayed: a new subscriber hears only later changes, so read the current values on load first.
    /// </summary>
    public interface IConfigChangedSource
    {
        /// <summary>Stream of saved configuration snapshots. Subscribers must dispose their subscription.</summary>
        IObservable<ClientConfigDto> ConfigChanged { get; }
    }
}
