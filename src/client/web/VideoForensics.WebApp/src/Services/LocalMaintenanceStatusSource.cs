using System.Reactive.Linq;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IMaintenanceStatusSource"/> for the WebApp. Reads the server's own maintenance gate, which is the source
    /// the broadcast is sent from.
    /// </summary>
    public sealed class LocalMaintenanceStatusSource(IMaintenanceGate gate) : IMaintenanceStatusSource
    {
        /// <inheritdoc />
        public IObservable<MaintenanceStatusSnapshot> Status =>
            gate.Select(status => new MaintenanceStatusSnapshot(status.Phase, status.Message));
    }
}
