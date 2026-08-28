using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for device capabilities (specs, features).</summary>
    public interface IDeviceCapabilitiesRepository
    {
        /// <summary>Gets capabilities by ID.</summary>
        Task<DeviceCapabilities?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets capabilities for a device.</summary>
        Task<DeviceCapabilities?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Adds new capabilities record.</summary>
        Task AddAsync(DeviceCapabilities capabilities, CancellationToken ct);

        /// <summary>Updates existing capabilities.</summary>
        Task UpdateAsync(DeviceCapabilities capabilities, CancellationToken ct);

        /// <summary>Deletes capabilities record.</summary>
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
