using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for time-series device health metrics.</summary>
    public interface IDeviceHealthRepository
    {
        /// <summary>Records a new device health metric.</summary>
        Task<DeviceHealth> AddAsync(DeviceHealth health, CancellationToken ct);

        /// <summary>Gets the latest health metric for a device.</summary>
        Task<DeviceHealth?> GetLatestAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets the full health history for a device, newest first.</summary>
        Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, CancellationToken ct);

        /// <summary>
        /// Gets a device's health history within a date range (inclusive of both ends), oldest
        /// first - the order a time-series chart (e.g. an RSSI-over-time plot) wants to consume.
        /// </summary>
        Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    }
}
