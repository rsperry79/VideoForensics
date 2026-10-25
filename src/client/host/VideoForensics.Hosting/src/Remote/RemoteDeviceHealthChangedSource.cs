using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// <see cref="IDeviceHealthChangedSource"/> for remote (MAUI) hosts. It forwards the server's device-health pushes from the
    /// shared <see cref="IRealtimeStore"/>, the only subscriber to the <c>DeviceHealthChanged</c> hub stream.
    /// </summary>
    public sealed class RemoteDeviceHealthChangedSource : IDeviceHealthChangedSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteDeviceHealthChangedSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<DeviceHealthChangedDto> DeviceHealthChanged => _store.DeviceHealthChanged;
    }
}
