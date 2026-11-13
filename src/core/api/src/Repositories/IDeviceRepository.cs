namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Read access to devices, with DTO signatures. Client hosts use this interface; the server implements it over
    /// the database and a Remote* class implements it over /api/v1/devices. Writes are server-only (IDeviceWriteRepository).
    /// </summary>
    public interface IDeviceRepository
    {
        /// <summary>Gets a device by ID, or null.</summary>
        Task<DeviceDto?> GetAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets all devices for a location.</summary>
        Task<IReadOnlyList<DeviceDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets a device by location ID and provider device ID, or null.</summary>
        Task<DeviceDto?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct);

        /// <summary>Lists all devices.</summary>
        Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken ct);
    }
}
