using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IEvidenceChangedSource"/> for remote (MAUI) hosts. It forwards the server's evidence-changed pushes from the
    /// shared <see cref="IRealtimeStore"/>, the only subscriber to the <c>EvidenceChanged</c> hub stream.
    /// </summary>
    public sealed class RemoteEvidenceChangedSource : IEvidenceChangedSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteEvidenceChangedSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<EvidenceChangedDto> EvidenceChanged => _store.EvidenceChanged;
    }
}
