using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for device entities.</summary>
    public interface IDeviceRepository
    {
        /// <summary>Gets a device by ID.</summary>
        Task<Device?> GetAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets all devices for a location.</summary>
        Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets a device by location ID and provider device ID.</summary>
        Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct);

        /// <summary>Lists all devices.</summary>
        Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct);

        /// <summary>Adds a new device.</summary>
        Task AddAsync(Device device, CancellationToken ct);

        /// <summary>Updates an existing device.</summary>
        Task UpdateAsync(Device device, CancellationToken ct);

        /// <summary>Updates the last successful pull timestamp for a device.</summary>
        Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct);

        /// <summary>Deletes a device.</summary>
        Task DeleteAsync(Guid deviceId, CancellationToken ct);
    }
}
