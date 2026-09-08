namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a device (camera, doorbell, etc.) linked to a location.
    /// </summary>
    /// <param name="Id">Unique identifier for the device.</param>
    /// <param name="LocationId">The location this device is associated with.</param>
    /// <param name="ProviderDeviceId">The provider's unique identifier for this device (e.g., Ring device ID).</param>
    /// <param name="Name">User-friendly name of the device (e.g., "Front Door Camera").</param>
    /// <param name="Type">Device type (e.g., "camera", "doorbell", "alarm_hub").</param>
    /// <param name="IsOnline">True if the device is currently online and reachable.</param>
    /// <param name="MetadataJson">Additional device metadata in JSON format, provider-specific.</param>
    /// <param name="LastSuccessfulPullAtUtc">Timestamp of the last successful data pull from the provider, in UTC. Null if never synced.</param>
    /// <param name="LastPullAttemptAtUtc">Timestamp of the last attempted data pull from the provider, in UTC. Null if never attempted.</param>
    /// <param name="TimeZoneId">IANA time zone identifier (e.g., "America/New_York") for the device's location.</param>
    /// <param name="LastSyncedUtc">Timestamp of the last successful sync operation, in UTC. Null if never synced.</param>
    /// <param name="SyncStatus">Current sync status: Pending, Synced, Stale, or Error.</param>
    /// <param name="ApiResponseHash">Hash of the last API response for change detection; null if not applicable.</param>
    public record DeviceDto(
        Guid Id,
        Guid LocationId,
        string ProviderDeviceId,
        string Name,
        string Type,
        bool IsOnline,
        string? MetadataJson,
        DateTime? LastSuccessfulPullAtUtc,
        DateTime? LastPullAttemptAtUtc,
        string? TimeZoneId,
        DateTime? LastSyncedUtc,
        SyncStatusDto SyncStatus,
        string? ApiResponseHash
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
                ApiResponseHash: entity.ApiResponseHash
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
                ApiResponseHash = dto.ApiResponseHash
            };
        }
    }
}
