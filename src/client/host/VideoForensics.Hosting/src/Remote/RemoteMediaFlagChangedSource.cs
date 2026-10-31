using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IMediaFlagChangedSource"/> for remote (MAUI) hosts. It forwards the server's media-flag pushes from the
    /// shared <see cref="IRealtimeStore"/>, the only subscriber to the <c>MediaFlagChanged</c> hub stream.
    /// </summary>
    public sealed class RemoteMediaFlagChangedSource : IMediaFlagChangedSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteMediaFlagChangedSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<MediaFlagChangedDto> MediaFlagChanged => _store.MediaFlagChanged;
    }
}
