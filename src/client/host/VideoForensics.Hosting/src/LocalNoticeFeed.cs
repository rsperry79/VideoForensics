using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// <see cref="INoticeFeed"/> for the WebApp. The dispatcher runs in the same process as the Blazor circuits, so it publishes
    /// each raised notice here and open Notices pages hear it directly.
    /// </summary>
    public sealed class LocalNoticeFeed : INoticeFeed, IDisposable
    {
        private readonly Subject<NotificationEvent> _notices = new();

        /// <inheritdoc />
        public IObservable<NotificationEvent> NewNotices => _notices.AsObservable();

        /// <summary>Publishes a notice that has just been raised.</summary>
        public void Publish(NotificationEvent notificationEvent)
        {
            _notices.OnNext(notificationEvent);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _notices.Dispose();
        }
    }
}
