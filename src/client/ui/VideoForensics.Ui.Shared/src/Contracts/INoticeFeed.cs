using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of new notices, so an open Notices page reloads without an operator pressing Refresh.
    /// The WebApp receives it in-process from the dispatcher; remote hosts (MAUI) forward the server's realtime push.
    /// Events are not replayed: a new subscriber hears only later notices.
    /// </summary>
    public interface INoticeFeed
    {
        /// <summary>Stream of newly raised notices. Subscribers must dispose their subscription.</summary>
        IObservable<NotificationEvent> NewNotices { get; }
    }
}
