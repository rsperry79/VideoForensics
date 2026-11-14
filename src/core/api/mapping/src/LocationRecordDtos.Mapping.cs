using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps the Location and OperatorPreferences entities to and from their DTOs.</summary>
    public static class LocationRecordDtoMapping
    {
        /// <summary>Converts a Location entity to a LocationRecordDto.</summary>
        public static LocationRecordDto ToDto(this Location entity) =>
            new(entity.Id, entity.ProviderLocationId, entity.Name, entity.Address, entity.LastSyncedUtc, (SyncStatusDto)entity.SyncStatus);

        /// <summary>Converts a LocationRecordDto to a Location entity.</summary>
        public static Location ToDomain(this LocationRecordDto dto) => new()
        {
            Id = dto.Id,
            ProviderLocationId = dto.ProviderLocationId,
            Name = dto.Name,
            Address = dto.Address,
            LastSyncedUtc = dto.LastSyncedUtc,
            SyncStatus = (SyncStatus)dto.SyncStatus,
        };

        /// <summary>Converts an OperatorPreferences entity to an OperatorPreferencesDto.</summary>
        public static OperatorPreferencesDto ToDto(this OperatorPreferences entity) =>
            new(entity.Id, entity.OperatorId, entity.ThemeMode, entity.CultureName, entity.UiMode, entity.UiModeLocked,
                entity.TimeFormat, entity.EncryptionWarningDismissed, entity.UpdatedAtUtc);

        /// <summary>Converts an OperatorPreferencesDto to an OperatorPreferences entity.</summary>
        public static OperatorPreferences ToDomain(this OperatorPreferencesDto dto) => new()
        {
            Id = dto.Id,
            OperatorId = dto.OperatorId,
            ThemeMode = dto.ThemeMode,
            CultureName = dto.CultureName,
            UiMode = dto.UiMode,
            UiModeLocked = dto.UiModeLocked,
            TimeFormat = dto.TimeFormat,
            EncryptionWarningDismissed = dto.EncryptionWarningDismissed,
            UpdatedAtUtc = dto.UpdatedAtUtc,
        };
    }
}
