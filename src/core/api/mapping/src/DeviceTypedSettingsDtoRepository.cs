using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side DTO view over the data-layer typed settings reader.</summary>
    public sealed class DeviceTypedSettingsDtoRepository(IDeviceTypedSettingsDataReader reader) : IDeviceTypedSettingsReader
    {
        /// <inheritdoc />
        public async Task<DeviceTypedSettingsDto> GetAsync(Guid deviceId, CancellationToken ct)
        {
            DeviceTypedSettings settings = await reader.GetAsync(deviceId, ct);
            return settings.ToDto(deviceId);
        }
    }
}
