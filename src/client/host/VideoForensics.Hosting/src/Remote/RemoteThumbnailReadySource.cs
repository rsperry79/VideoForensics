using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IThumbnailReadySource"/> for remote (MAUI) hosts. It forwards the server's thumbnail-ready events from
    /// the shared <see cref="IRealtimeStore"/>, the only subscriber to the <c>ThumbnailReady</c> hub stream.
    /// No mapping is needed: Ui.Shared uses the wire <see cref="ThumbnailReadyDto"/> directly.
    /// </summary>
    public sealed class RemoteThumbnailReadySource : IThumbnailReadySource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteThumbnailReadySource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<ThumbnailReadyDto> ThumbnailReady => _store.ThumbnailReady;
    }
}
