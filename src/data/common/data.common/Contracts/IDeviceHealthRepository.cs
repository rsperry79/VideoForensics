using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for current device health state (battery, WiFi, connectivity).</summary>
    public interface IDeviceHealthRepository
    {
        /// <summary>Gets health record by ID.</summary>
        Task<DeviceHealth?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets health record for a device.</summary>
        Task<DeviceHealth?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Adds new health record.</summary>
        Task AddAsync(DeviceHealth health, CancellationToken ct);

        /// <summary>Updates existing health record.</summary>
        Task UpdateAsync(DeviceHealth health, CancellationToken ct);

        /// <summary>Deletes health record.</summary>
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
