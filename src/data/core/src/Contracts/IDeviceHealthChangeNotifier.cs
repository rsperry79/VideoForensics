namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>
    /// Announces that a device health reading was recorded, so open device views can refresh their status.
    /// Hosts that serve no live views use <see cref="NullDeviceHealthChangeNotifier"/>; the WebApp replaces it with a SignalR push.
    /// Implementations must not throw: the write that triggered the call has already succeeded.
    /// </summary>
    public interface IDeviceHealthChangeNotifier
    {
        /// <summary>Reports that a health reading was recorded for <paramref name="deviceId"/>.</summary>
        Task DeviceHealthRecordedAsync(Guid deviceId, CancellationToken ct);
    }
}
