using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// <see cref="IDeviceHealthChangedSource"/> for the WebApp, whose Blazor circuits run in the same process as the writers.
    /// It reads the in-process <see cref="DeviceHealthChangedBus"/>, so it needs no hub connection.
    /// </summary>
    public sealed class LocalDeviceHealthChangedSource : IDeviceHealthChangedSource
    {
        private readonly DeviceHealthChangedBus _bus;

        /// <summary>Creates the source over the singleton bus.</summary>
        public LocalDeviceHealthChangedSource(DeviceHealthChangedBus bus)
        {
            _bus = bus;
        }

        /// <inheritdoc />
        public IObservable<DeviceHealthChangedDto> DeviceHealthChanged => _bus.Events;
    }
}
