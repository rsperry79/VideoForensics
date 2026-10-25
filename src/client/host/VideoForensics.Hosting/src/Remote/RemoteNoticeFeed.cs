using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="INoticeFeed"/> for remote (MAUI) hosts. It forwards the server's urgent-event pushes from the hub. The hub is
    /// subscribed directly rather than the realtime store, because the store replays its latest value to a new subscriber and
    /// an open Notices page would reload for an alert it has already shown.
    /// </summary>
    public sealed class RemoteNoticeFeed : INoticeFeed
    {
        private readonly IRealtimeHub _hub;

        /// <summary>Creates the feed over the singleton realtime hub.</summary>
        public RemoteNoticeFeed(IRealtimeHub hub)
        {
            _hub = hub;
        }

        /// <inheritdoc />
        public IObservable<NotificationEvent> NewNotices => _hub.UrgentEvents;
    }
}
