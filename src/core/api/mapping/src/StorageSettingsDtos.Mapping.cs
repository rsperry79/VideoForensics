using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping storage settings domain types to/from DTOs.</summary>
    public static class StorageSettingsDtoMapping
    {
        /// <summary>
        /// Converts a domain StorageCategoryStatus to a StorageCategoryStatusDto.
        /// </summary>
        public static StorageCategoryStatusDto ToDto(this StorageCategoryStatus status)
        {
            return new StorageCategoryStatusDto(
                Category: status.Category,
                CurrentPath: status.CurrentPath,
                IsDefault: status.IsDefault,
                UsedBytes: status.UsedBytes,
                FreeBytes: status.FreeBytes,
                IsRelocatable: status.IsRelocatable,
                RequiresRestart: status.RequiresRestart
            );
        }

        /// <summary>
        /// Converts a StorageCategoryStatusDto to a domain StorageCategoryStatus.
        /// </summary>
        public static StorageCategoryStatus ToDomain(this StorageCategoryStatusDto dto)
        {
            return new StorageCategoryStatus(
                Category: dto.Category,
                CurrentPath: dto.CurrentPath,
                IsDefault: dto.IsDefault,
                UsedBytes: dto.UsedBytes,
                FreeBytes: dto.FreeBytes,
                IsRelocatable: dto.IsRelocatable,
                RequiresRestart: dto.RequiresRestart
            );
        }

        /// <summary>
        /// Converts a domain StorageSettings to a StorageSettingsDto.
        /// </summary>
        public static StorageSettingsDto ToDto(this StorageSettings settings)
        {
            return new StorageSettingsDto(
                Categories: settings.Categories.Select(c => c.ToDto()).ToList(),
                DefaultRoot: settings.DefaultRoot
            );
        }

        /// <summary>
        /// Converts a StorageSettingsDto to a domain StorageSettings.
        /// </summary>
        public static StorageSettings ToDomain(this StorageSettingsDto dto)
        {
            return new StorageSettings(
                Categories: dto.Categories.Select(c => c.ToDomain()).ToList(),
                DefaultRoot: dto.DefaultRoot
            );
        }

        /// <summary>
        /// Converts a domain RelocateStorageCategoryRequest to a RelocateStorageCategoryRequestDto.
        /// </summary>
        public static RelocateStorageCategoryRequestDto ToDto(this RelocateStorageCategoryRequest request)
        {
            return new RelocateStorageCategoryRequestDto(
                Category: request.Category,
                NewRootPath: request.NewRootPath
            );
        }

        /// <summary>
        /// Converts a RelocateStorageCategoryRequestDto to a domain RelocateStorageCategoryRequest.
        /// </summary>
        public static RelocateStorageCategoryRequest ToDomain(this RelocateStorageCategoryRequestDto dto)
        {
            return new RelocateStorageCategoryRequest(
                Category: dto.Category,
                NewRootPath: dto.NewRootPath
            );
        }

        /// <summary>
        /// Converts a domain RelocateStorageCategoryResult to a RelocateStorageCategoryResultDto.
        /// </summary>
        public static RelocateStorageCategoryResultDto ToDto(this RelocateStorageCategoryResult result)
        {
            return new RelocateStorageCategoryResultDto(
                Succeeded: result.Succeeded,
                ErrorMessage: result.ErrorMessage,
                RestartRequired: result.RestartRequired
            );
        }

        /// <summary>
        /// Converts a RelocateStorageCategoryResultDto to a domain RelocateStorageCategoryResult.
        /// </summary>
        public static RelocateStorageCategoryResult ToDomain(this RelocateStorageCategoryResultDto dto)
        {
            return new RelocateStorageCategoryResult(
                Succeeded: dto.Succeeded,
                ErrorMessage: dto.ErrorMessage,
                RestartRequired: dto.RestartRequired
            );
        }
    }
}
