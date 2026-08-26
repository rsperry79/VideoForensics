namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic event and configuration interface</summary>
    public interface IEventAndConfigService
    {
        /// <summary>Gets events (motion, person detection, etc.) for a device</summary>
        Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
            string deviceId,
            DateTime startDate,
            DateTime endDate,
            string? eventType = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>Gets device configuration settings</summary>
        Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken cancellationToken = default);

        /// <summary>Updates device configuration settings</summary>
        Task<bool> UpdateDeviceConfigAsync(string deviceId, DeviceConfig config, CancellationToken cancellationToken = default);
    }

    /// <summary>Represents a device event (motion, person detection, etc.)</summary>
    public record DeviceEvent(
        string Id,
        string DeviceId,
        string EventType, // "motion", "person", "sound", etc.
        DateTime Timestamp,
        string? SnapshotUrl = null,
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>Device configuration settings</summary>
    public record DeviceConfig(
        string DeviceId,
        bool MotionDetectionEnabled,
        int MotionSensitivity, // 0-100
        string? RecordingMode, // "always", "motion", "off"
        Dictionary<string, object>? CustomSettings = null
    );
}
