using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Writes a device's latest capability, feature and alert rows, and links the Devices row to its
    /// latest health and feature rows. Each call is keyed by the real Devices.Id.
    /// </summary>
    public interface IDeviceDetailsRepository
    {
        /// <summary>
        /// Upserts the given rows for the device. Capabilities and features are one row per device and are
        /// updated in place; alerts are one row per (device, alert type). Null inputs are not written.
        /// Writes nothing when the device does not exist.
        /// </summary>
        Task UpsertAsync(
            Guid deviceId,
            Guid? healthId,
            DeviceCapabilities? capabilities,
            DeviceFeatures? features,
            IReadOnlyList<DeviceAlerts>? alerts,
            CancellationToken ct);
    }
}
