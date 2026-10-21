using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// In-process event stream of produced thumbnails. The WebApp's own Blazor circuits read it directly, since they
    /// run in the same process as the generator and need no hub round trip. It is an event stream with no replay.
    /// </summary>
    public sealed class ThumbnailReadyBus
    {
        private readonly object _gate = new();
        private readonly Subject<ThumbnailReadyDto> _events = new();

        /// <summary>Thumbnails produced from now on. A subscriber does not receive earlier ones.</summary>
        public IObservable<ThumbnailReadyDto> Events => _events.AsObservable();

        /// <summary>Publishes one produced thumbnail to current subscribers.</summary>
        public void Publish(ThumbnailReadyDto ready)
        {
            // Subject.OnNext is not thread-safe, and the generator may run on any thread-pool thread.
            lock (_gate)
            {
                _events.OnNext(ready);
            }
        }
    }
}
