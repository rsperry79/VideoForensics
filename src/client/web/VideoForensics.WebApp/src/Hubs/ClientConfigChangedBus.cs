using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// In-process event stream of saved settings. The WebApp's own Blazor circuits read it directly, since they run in
    /// the same process as the writers and need no hub round trip. It is an event stream with no replay.
    /// </summary>
    public sealed class ClientConfigChangedBus
    {
        private readonly object _gate = new();
        private readonly Subject<ClientConfigDto> _events = new();

        /// <summary>Saves made from now on. A subscriber does not receive earlier ones.</summary>
        public IObservable<ClientConfigDto> Events => _events.AsObservable();

        /// <summary>Publishes one saved snapshot to current subscribers.</summary>
        public void Publish(ClientConfigDto change)
        {
            // Subject.OnNext is not thread-safe, and saves may run on any thread-pool thread.
            lock (_gate)
            {
                _events.OnNext(change);
            }
        }
    }
}
