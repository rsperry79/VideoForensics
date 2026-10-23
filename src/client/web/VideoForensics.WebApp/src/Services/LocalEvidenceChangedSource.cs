using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IEvidenceChangedSource"/> for the WebApp, whose Blazor circuits run in the same process as the writers.
    /// It reads the in-process <see cref="EvidenceChangedBus"/>, so it needs no hub connection.
    /// </summary>
    public sealed class LocalEvidenceChangedSource : IEvidenceChangedSource
    {
        private readonly EvidenceChangedBus _bus;

        /// <summary>Creates the source over the singleton bus.</summary>
        public LocalEvidenceChangedSource(EvidenceChangedBus bus)
        {
            _bus = bus;
        }

        /// <inheritdoc />
        public IObservable<EvidenceChangedDto> EvidenceChanged => _bus.Events;
    }
}
