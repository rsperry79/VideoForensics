using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "device health changed" events, so an open Devices tab refreshes its uptime status without a reload.
    /// The WebApp receives it in-process from the writers; remote hosts (MAUI) forward the server's realtime push.
    /// Events are not replayed: a new subscriber hears only later changes.
    /// </summary>
    public interface IDeviceHealthChangedSource
    {
        /// <summary>Stream of device health changes. Subscribers must dispose their subscription.</summary>
        IObservable<DeviceHealthChangedDto> DeviceHealthChanged { get; }
    }
}
