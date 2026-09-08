namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a location/site (home, office, etc.) obtained from provider discovery.
    /// </summary>
    /// <param name="Id">Provider-specific location identifier.</param>
    /// <param name="Name">User-friendly name of the location.</param>
    /// <param name="Address">Physical address of the location, if available.</param>
    /// <param name="Metadata">Additional location metadata in key-value format, provider-specific.</param>
    public record LocationDto(
        string Id,
        string Name,
        string? Address,
        Dictionary<string, string>? Metadata
    );

    /// <summary>
    /// Data transfer object for a device (camera, doorbell, etc.) obtained from provider discovery.
    /// </summary>
    /// <param name="Id">Provider-specific device identifier.</param>
    /// <param name="Name">User-friendly name of the device.</param>
    /// <param name="Type">Device type (e.g., "camera", "doorbell", "base_station").</param>
    /// <param name="LocationId">Identifier of the location this device belongs to.</param>
    /// <param name="IsOnline">True if the device is currently online and reachable.</param>
    /// <param name="Metadata">Additional device metadata in key-value format, provider-specific.</param>
    public record DiscoveryDeviceDto(
        string Id,
        string Name,
        string Type,
        string LocationId,
        bool IsOnline,
        Dictionary<string, string>? Metadata
    );

    /// <summary>
    /// Data transfer object for a device event (motion, person detection, etc.) obtained from provider configuration/events API.
    /// </summary>
    /// <param name="Id">Provider-specific event identifier.</param>
    /// <param name="DeviceId">Identifier of the device that detected this event.</param>
    /// <param name="EventType">Type of event (e.g., "motion", "person", "sound").</param>
    /// <param name="Timestamp">Timestamp when the event occurred at the device.</param>
    /// <param name="SnapshotUrl">URL to a snapshot image captured during the event, if available.</param>
    /// <param name="Metadata">Additional event metadata in key-value format, provider-specific.</param>
    public record DeviceEventDto(
        string Id,
        string DeviceId,
        string EventType,
        DateTime Timestamp,
        string? SnapshotUrl,
        Dictionary<string, string>? Metadata
    );

    /// <summary>
    /// Data transfer object for device configuration settings obtained from provider configuration API.
    /// </summary>
    /// <param name="DeviceId">Identifier of the device this configuration applies to.</param>
    /// <param name="MotionDetectionEnabled">True if motion detection is enabled on the device.</param>
    /// <param name="MotionSensitivity">Motion detection sensitivity level (0-100).</param>
    /// <param name="RecordingMode">Recording mode setting (e.g., "always", "motion", "off").</param>
    /// <param name="CustomSettings">Additional provider-specific configuration settings.</param>
    public record DeviceConfigDto(
        string DeviceId,
        bool MotionDetectionEnabled,
        int MotionSensitivity,
        string? RecordingMode,
        Dictionary<string, object>? CustomSettings
    );

    /// <summary>Extension methods for mapping provider contract models to DTOs.</summary>
    public static class DiscoveryDtoMapping
    {
        /// <summary>
        /// Converts a Location from provider contracts to a LocationDto.
        /// </summary>
        public static LocationDto ToDto(this VideoForensics.Providers.Common.Contracts.Location location)
        {
            return new LocationDto(
                Id: location.Id,
                Name: location.Name,
                Address: location.Address,
                Metadata: location.Metadata
            );
        }

        /// <summary>
        /// Converts a LocationDto to a Location provider contract model.
        /// </summary>
        public static VideoForensics.Providers.Common.Contracts.Location ToDomain(this LocationDto dto)
        {
            return new VideoForensics.Providers.Common.Contracts.Location(
                Id: dto.Id,
                Name: dto.Name,
                Address: dto.Address,
                Metadata: dto.Metadata
            );
        }

        /// <summary>
        /// Converts a Device from provider contracts to a DiscoveryDeviceDto.
        /// </summary>
        public static DiscoveryDeviceDto ToDto(this VideoForensics.Providers.Common.Contracts.Device device)
        {
            return new DiscoveryDeviceDto(
                Id: device.Id,
                Name: device.Name,
                Type: device.Type,
                LocationId: device.LocationId,
                IsOnline: device.IsOnline,
                Metadata: device.Metadata
            );
        }

        /// <summary>
        /// Converts a DiscoveryDeviceDto to a Device provider contract model.
        /// </summary>
        public static VideoForensics.Providers.Common.Contracts.Device ToDomain(this DiscoveryDeviceDto dto)
        {
            return new VideoForensics.Providers.Common.Contracts.Device(
                Id: dto.Id,
                Name: dto.Name,
                Type: dto.Type,
                LocationId: dto.LocationId,
                IsOnline: dto.IsOnline,
                Metadata: dto.Metadata
            );
        }

        /// <summary>
        /// Converts a DeviceEvent from provider contracts to a DeviceEventDto.
        /// </summary>
        public static DeviceEventDto ToDto(this VideoForensics.Providers.Common.Contracts.DeviceEvent @event)
        {
            return new DeviceEventDto(
                Id: @event.Id,
                DeviceId: @event.DeviceId,
                EventType: @event.EventType,
                Timestamp: @event.Timestamp,
                SnapshotUrl: @event.SnapshotUrl,
                Metadata: @event.Metadata
            );
        }

        /// <summary>
        /// Converts a DeviceEventDto to a DeviceEvent provider contract model.
        /// </summary>
        public static VideoForensics.Providers.Common.Contracts.DeviceEvent ToDomain(this DeviceEventDto dto)
        {
            return new VideoForensics.Providers.Common.Contracts.DeviceEvent(
                Id: dto.Id,
                DeviceId: dto.DeviceId,
                EventType: dto.EventType,
                Timestamp: dto.Timestamp,
                SnapshotUrl: dto.SnapshotUrl,
                Metadata: dto.Metadata
            );
        }

        /// <summary>
        /// Converts a DeviceConfig from provider contracts to a DeviceConfigDto.
        /// </summary>
        public static DeviceConfigDto ToDto(this VideoForensics.Providers.Common.Contracts.DeviceConfig config)
        {
            return new DeviceConfigDto(
                DeviceId: config.DeviceId,
                MotionDetectionEnabled: config.MotionDetectionEnabled,
                MotionSensitivity: config.MotionSensitivity,
                RecordingMode: config.RecordingMode,
                CustomSettings: config.CustomSettings
            );
        }

        /// <summary>
        /// Converts a DeviceConfigDto to a DeviceConfig provider contract model.
        /// </summary>
        public static VideoForensics.Providers.Common.Contracts.DeviceConfig ToDomain(this DeviceConfigDto dto)
        {
            return new VideoForensics.Providers.Common.Contracts.DeviceConfig(
                DeviceId: dto.DeviceId,
                MotionDetectionEnabled: dto.MotionDetectionEnabled,
                MotionSensitivity: dto.MotionSensitivity,
                RecordingMode: dto.RecordingMode,
                CustomSettings: dto.CustomSettings
            );
        }
    }
}
