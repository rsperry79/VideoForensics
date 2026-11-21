using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Reads the typed settings rows stored for a device, as DTOs.</summary>
    public interface IDeviceTypedSettingsReader
    {
        /// <summary>Returns the device's typed settings rows. Empty groups when none are stored.</summary>
        Task<DeviceTypedSettingsDto> GetAsync(Guid deviceId, CancellationToken ct);
    }
}
