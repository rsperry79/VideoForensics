using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IThumbnailReadySource"/> for the WebApp, whose Blazor circuits run in the same process as the thumbnail
    /// generator. It reads the in-process <see cref="ThumbnailReadyBus"/>, so it needs no hub connection.
    /// </summary>
    public sealed class LocalThumbnailReadySource : IThumbnailReadySource
    {
        private readonly ThumbnailReadyBus _bus;

        /// <summary>Creates the source over the singleton bus.</summary>
        public LocalThumbnailReadySource(ThumbnailReadyBus bus)
        {
            _bus = bus;
        }

        /// <inheritdoc />
        public IObservable<ThumbnailReadyDto> ThumbnailReady => _bus.Events;
    }
}
