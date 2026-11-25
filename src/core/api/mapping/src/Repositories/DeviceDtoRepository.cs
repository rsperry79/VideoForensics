using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Server-side <see cref="IDeviceRepository"/> that reads devices through <see cref="IDeviceWriteRepository"/> and maps
    /// each entity to a <see cref="DeviceDto"/>. Lets server code expose the DTO read surface without a second store.
    /// </summary>
    public sealed class DeviceDtoRepository(IDeviceWriteRepository devices) : IDeviceRepository
    {
        /// <inheritdoc />
        public async Task<DeviceDto?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            VideoForensics.Data.Common.Entities.Device? device = await devices.GetAsync(deviceId, ct);
            return device?.ToDto();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devicesAtLocation = await devices.GetByLocationIdAsync(locationId, ct);
            return devicesAtLocation.Select(d => d.ToDto()).ToList();
        }

        /// <inheritdoc />
        public async Task<DeviceDto?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct)
        {
            VideoForensics.Data.Common.Entities.Device? device = await devices.GetByProviderDeviceIdAsync(locationId, providerDeviceId, ct);
            return device?.ToDto();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken ct)
        {
            IReadOnlyList<VideoForensics.Data.Common.Entities.Device> all = await devices.ListAsync(ct);
            return all.Select(d => d.ToDto()).ToList();
        }
    }
}
