using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
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
                LastSuccessfulPullAtUtc: entity.LastSuccessfulPullAtUtc,
                LastPullAttemptAtUtc: entity.LastPullAttemptAtUtc,
                TimeZoneId: entity.TimeZoneId,
                LastSyncedUtc: entity.LastSyncedUtc,
                SyncStatus: (SyncStatusDto)entity.SyncStatus,
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
                ProviderName: entity.ProviderName,
                SettingsMetadataJson: entity.SettingsMetadataJson
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
                LastSuccessfulPullAtUtc = dto.LastSuccessfulPullAtUtc,
                LastPullAttemptAtUtc = dto.LastPullAttemptAtUtc,
                TimeZoneId = dto.TimeZoneId,
                LastSyncedUtc = dto.LastSyncedUtc,
                SyncStatus = (VideoForensics.Data.Common.Entities.SyncStatus)dto.SyncStatus,
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
                ProviderName = dto.ProviderName,
                SettingsMetadataJson = dto.SettingsMetadataJson
            };
        }
    }
}
