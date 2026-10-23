using System.Reactive.Linq;
using System.Reactive.Subjects;

using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// In-process event stream of linked accounts. Registered SCOPED per client circuit, not singleton, so one user's
    /// link never reaches another user's Blazor circuit in the WebApp. It is an event stream with no replay.
    /// </summary>
    public sealed class AccountLinkedBus : IAccountLinkedSource, IAccountLinkedNotifier
    {
        private readonly object _gate = new();
        private readonly Subject<Guid> _events = new();

        /// <summary>Accounts linked from now on. A subscriber does not receive earlier ones.</summary>
        public IObservable<Guid> AccountLinked => _events.AsObservable();

        /// <summary>Publishes one linked account id to current subscribers.</summary>
        public void Publish(Guid providerAccountId)
        {
            // Subject.OnNext is not thread-safe, and the link may complete on any thread-pool thread.
            lock (_gate)
            {
                _events.OnNext(providerAccountId);
            }
        }
    }
}
