using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>The typed settings rows currently stored for one device.</summary>
    /// <param name="OfflineMotion">The offline motion settings row, or null when none is stored.</param>
    /// <param name="DetectionTypes">One row per CV detection type.</param>
    /// <param name="Thresholds">One row per numeric CV threshold.</param>
    public sealed record DeviceTypedSettings(
        DeviceOfflineMotionSettings? OfflineMotion,
        IReadOnlyList<DeviceDetectionType> DetectionTypes,
        IReadOnlyList<DeviceDetectionThreshold> Thresholds);

    /// <summary>Reads the typed settings rows stored for a device.</summary>
    public interface IDeviceTypedSettingsReader
    {
        /// <summary>Returns the device's typed settings rows. Empty groups when none are stored.</summary>
        Task<DeviceTypedSettings> GetAsync(Guid deviceId, CancellationToken ct);
    }
}
