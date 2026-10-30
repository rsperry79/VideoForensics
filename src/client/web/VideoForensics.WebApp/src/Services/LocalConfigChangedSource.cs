using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IConfigChangedSource"/> for the WebApp, whose Blazor circuits run in the same process as the writers.
    /// It reads the in-process <see cref="ClientConfigChangedBus"/>, so it needs no hub connection.
    /// </summary>
    public sealed class LocalConfigChangedSource : IConfigChangedSource
    {
        private readonly ClientConfigChangedBus _bus;

        /// <summary>Creates the source over the singleton bus.</summary>
        public LocalConfigChangedSource(ClientConfigChangedBus bus)
        {
            _bus = bus;
        }

        /// <inheritdoc />
        public IObservable<ClientConfigDto> ConfigChanged => _bus.Events;
    }
}
