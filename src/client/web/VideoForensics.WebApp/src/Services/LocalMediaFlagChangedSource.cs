using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IMediaFlagChangedSource"/> for the WebApp, whose Blazor circuits run in the same process as the endpoints.
    /// It reads the in-process <see cref="MediaFlagChangedBus"/>, so it needs no hub connection.
    /// </summary>
    public sealed class LocalMediaFlagChangedSource : IMediaFlagChangedSource
    {
        private readonly MediaFlagChangedBus _bus;

        /// <summary>Creates the source over the singleton bus.</summary>
        public LocalMediaFlagChangedSource(MediaFlagChangedBus bus)
        {
            _bus = bus;
        }

        /// <inheritdoc />
        public IObservable<MediaFlagChangedDto> MediaFlagChanged => _bus.Events;
    }
}
