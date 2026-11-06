using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IConfigChangedSource"/> for remote (MAUI) hosts. It forwards the server's saved-settings pushes from the
    /// shared <see cref="IRealtimeStore"/>, the only subscriber to the <c>ClientConfigChanged</c> hub stream.
    /// </summary>
    public sealed class RemoteConfigChangedSource : IConfigChangedSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteConfigChangedSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<ClientConfigDto> ConfigChanged => _store.ClientConfigChanged;
    }
}
