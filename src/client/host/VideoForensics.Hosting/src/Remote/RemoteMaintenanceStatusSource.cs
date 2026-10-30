using System.Reactive.Linq;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IMaintenanceStatusSource"/> for remote (MAUI) hosts. Forwards the server's maintenance broadcast from the
    /// shared <see cref="IRealtimeStore"/> and maps it to the UI's snapshot, so Ui.Shared never sees the wire type.
    /// </summary>
    public sealed class RemoteMaintenanceStatusSource(IRealtimeStore store) : IMaintenanceStatusSource
    {
        /// <inheritdoc />
        public IObservable<MaintenanceStatusSnapshot> Status => store.Maintenance.Select(ToSnapshot);

        private static MaintenanceStatusSnapshot ToSnapshot(MaintenanceStatusDto status) => new(status.Phase, status.Message);
    }
}
