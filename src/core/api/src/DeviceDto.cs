namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a device (camera, doorbell, etc.) linked to a location.
    /// </summary>
    /// <param name="Id">Unique identifier for the device.</param>
    /// <param name="LocationId">The location this device is associated with.</param>
    /// <param name="ProviderDeviceId">The provider's unique identifier for this device (e.g., Ring device ID).</param>
    /// <param name="Name">User-friendly name of the device (e.g., "Front Door Camera").</param>
    /// <param name="Type">Device type (e.g., "camera", "doorbell", "alarm_hub").</param>
    /// <param name="IsOnline">True if online, False if offline, Null if the provider gave no state.</param>
    /// <param name="MetadataJson">Additional device metadata in JSON format, provider-specific.</param>
    /// <param name="LastSuccessfulPullAtUtc">Timestamp of the last successful data pull from the provider, in UTC. Null if never synced.</param>
    /// <param name="LastPullAttemptAtUtc">Timestamp of the last attempted data pull from the provider, in UTC. Null if never attempted.</param>
    /// <param name="TimeZoneId">IANA time zone identifier (e.g., "America/New_York") for the device's location.</param>
    /// <param name="LastSyncedUtc">Timestamp of the last successful sync operation, in UTC. Null if never synced.</param>
    /// <param name="SyncStatus">Current sync status: Pending, Synced, Stale, or Error.</param>
    /// <param name="ApiResponseHash">Hash of the last API response for change detection; null if not applicable.</param>
    /// <param name="RingDeviceId">Ring's hex device_id. Null if the provider gave none.</param>
    /// <param name="RingKind">Raw Ring device kind, e.g. doorbell_v4. Null if unknown.</param>
    /// <param name="FirmwareVersion">Device firmware version. Null if unknown.</param>
    /// <param name="Latitude">Device latitude. Null if unknown.</param>
    /// <param name="Longitude">Device longitude. Null if unknown.</param>
    /// <param name="Address">The device's own address field. Null if unknown.</param>
    /// <param name="Subscribed">True if subscribed to Ring service. Null if unknown.</param>
    /// <param name="SubscribedMotions">True if subscribed to motion events. Null if unknown.</param>
    /// <param name="BatteryLife">Ring's raw battery_life value, kept as text. Null if unknown.</param>
    /// <param name="ExternalConnection">True if externally connected (wired). Null if unknown.</param>
    /// <param name="Owned">True if the device is owned by the account. Null if unknown.</param>
    /// <param name="Stolen">True if the device is flagged as stolen. Null if unknown.</param>
    public record DeviceDto(
        Guid Id,
        Guid LocationId,
        string ProviderDeviceId,
        string Name,
        string Type,
        bool? IsOnline,
        string? MetadataJson,
        DateTime? LastSuccessfulPullAtUtc,
        DateTime? LastPullAttemptAtUtc,
        string? TimeZoneId,
        DateTime? LastSyncedUtc,
        SyncStatusDto SyncStatus,
        string? ApiResponseHash,
        string? RingDeviceId = null,
        string? RingKind = null,
        string? FirmwareVersion = null,
        double? Latitude = null,
        double? Longitude = null,
        string? Address = null,
        bool? Subscribed = null,
        bool? SubscribedMotions = null,
        string? BatteryLife = null,
        bool? ExternalConnection = null,
        bool? Owned = null,
        bool? Stolen = null,
        string? ProviderName = null
    );

    /// <summary>Sync status enumeration for a device.</summary>
    public enum SyncStatusDto
    {
        /// <summary>Device has not been synced yet or sync is in progress.</summary>
        Pending,
        /// <summary>Device data is synchronized with the provider.</summary>
        Synced,
        /// <summary>Device data is synchronized but may be outdated.</summary>
        Stale,
        /// <summary>An error occurred during the last sync attempt.</summary>
        Error
    }

    /// <summary>Extension methods for mapping Device entities to/from DeviceDtos.</summary>
    public static class DeviceDtoMapping
    {
        /// <summary>
        /// Converts a Device entity to a DeviceDto.
        /// </summary>
        public static DeviceDto ToDto(this VideoForensics.Data.Common.Entities.Device entity)
        {
            return new DeviceDto(
                Id: entity.Id,
                LocationId: entity.LocationId,
                ProviderDeviceId: entity.ProviderDeviceId,
                Name: entity.Name,
                Type: entity.Type,
                IsOnline: entity.IsOnline,
                MetadataJson: entity.MetadataJson,
                LastSuccessfulPullAtUtc: entity.LastSuccessfulPullAtUtc,
                LastPullAttemptAtUtc: entity.LastPullAttemptAtUtc,
                TimeZoneId: entity.TimeZoneId,
                LastSyncedUtc: entity.LastSyncedUtc,
                SyncStatus: (SyncStatusDto)entity.SyncStatus,
                ApiResponseHash: entity.ApiResponseHash,
                RingDeviceId: entity.RingDeviceId,
                RingKind: entity.RingKind,
                FirmwareVersion: entity.FirmwareVersion,
                Latitude: entity.Latitude,
                Longitude: entity.Longitude,
                Address: entity.Address,
                Subscribed: entity.Subscribed,
                SubscribedMotions: entity.SubscribedMotions,
                BatteryLife: entity.BatteryLife,
                ExternalConnection: entity.ExternalConnection,
                Owned: entity.Owned,
                Stolen: entity.Stolen,
                ProviderName: entity.ProviderName
            );
        }

        /// <summary>
        /// Converts a DeviceDto to a Device entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.Device ToDomain(this DeviceDto dto)
        {
            return new VideoForensics.Data.Common.Entities.Device
            {
                Id = dto.Id,
                LocationId = dto.LocationId,
                ProviderDeviceId = dto.ProviderDeviceId,
                Name = dto.Name,
                Type = dto.Type,
                IsOnline = dto.IsOnline,
                MetadataJson = dto.MetadataJson,
                LastSuccessfulPullAtUtc = dto.LastSuccessfulPullAtUtc,
                LastPullAttemptAtUtc = dto.LastPullAttemptAtUtc,
                TimeZoneId = dto.TimeZoneId,
                LastSyncedUtc = dto.LastSyncedUtc,
                SyncStatus = (VideoForensics.Data.Common.Entities.SyncStatus)dto.SyncStatus,
                ApiResponseHash = dto.ApiResponseHash,
                RingDeviceId = dto.RingDeviceId,
                RingKind = dto.RingKind,
                FirmwareVersion = dto.FirmwareVersion,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Address = dto.Address,
                Subscribed = dto.Subscribed,
                SubscribedMotions = dto.SubscribedMotions,
                BatteryLife = dto.BatteryLife,
                ExternalConnection = dto.ExternalConnection,
                Owned = dto.Owned,
                Stolen = dto.Stolen,
                ProviderName = dto.ProviderName
            };
        }
    }
}
